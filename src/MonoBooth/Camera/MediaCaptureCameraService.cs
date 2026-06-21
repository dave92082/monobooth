using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;
using Windows.Media.MediaProperties;

namespace MonoBooth.Camera;

/// <summary>
/// Live camera backed by the modern Windows <see cref="MediaCapture"/> / <see cref="MediaFrameReader"/>
/// stack. We pull frames as CPU-side <see cref="Windows.Graphics.Imaging.SoftwareBitmap"/>s and keep
/// the most recent one converted to a GDI+ <see cref="Bitmap"/> for the UI and for captures.
/// </summary>
public sealed class MediaCaptureCameraService : ICameraService
{
    private readonly string _preferredCamera;
    private readonly object _sync = new();

    private MediaCapture? _mediaCapture;
    private MediaFrameReader? _reader;
    private Bitmap? _latest;
    private bool _disposed;
    private bool _firstFrameLogged;
    private bool _conversionErrorLogged;

    // Native subtypes we can use directly, best first. Anything else (MJPG, H264, …) is compressed
    // and would hand us a null SoftwareBitmap, so we steer the source onto one of these.
    private static readonly string[] PreferredSubtypes =
        { "NV12", "YUY2", "UYVY", "RGB24", "ARGB32", "RGB32", "BGRA8" };

    private static readonly string LogPath =
        Path.Combine(AppContext.BaseDirectory, "monobooth-camera.log");

    public MediaCaptureCameraService(string preferredCamera = "")
    {
        _preferredCamera = preferredCamera ?? "";
    }

    public event EventHandler? FrameReady;

    public string? DeviceName { get; private set; }

    public bool IsRunning { get; private set; }

    public async Task<bool> StartAsync()
    {
        try
        {
            var groups = await MediaFrameSourceGroup.FindAllAsync();
            if (groups.Count == 0)
                return false;

            var group = SelectGroup(groups);

            _mediaCapture = new MediaCapture();
            await _mediaCapture.InitializeAsync(new MediaCaptureInitializationSettings
            {
                SourceGroup = group,
                SharingMode = MediaCaptureSharingMode.ExclusiveControl,
                // Cpu memory guarantees the frames expose a SoftwareBitmap we can read.
                MemoryPreference = MediaCaptureMemoryPreference.Cpu,
                StreamingCaptureMode = StreamingCaptureMode.Video,
            });

            var colorSource = _mediaCapture.FrameSources.Values
                .FirstOrDefault(s => s.Info.SourceKind == MediaFrameSourceKind.Color)
                ?? _mediaCapture.FrameSources.Values.FirstOrDefault();

            if (colorSource is null)
            {
                Log("No frame source found.");
                return false;
            }

            DeviceName = group.DisplayName;
            Log($"Camera: {group.DisplayName}");

            await TrySelectUncompressedFormat(colorSource);

            // Ask the reader to deliver BGRA8 frames. The capture pipeline transcodes from whatever
            // the camera streams natively (NV12/YUY2/MJPG/…), so we always get a usable SoftwareBitmap.
            try
            {
                _reader = await _mediaCapture.CreateFrameReaderAsync(colorSource, MediaEncodingSubtypes.Bgra8);
                Log("Frame reader requesting BGRA8 output.");
            }
            catch (Exception ex) when (ex is COMException or ArgumentException or InvalidOperationException)
            {
                _reader = await _mediaCapture.CreateFrameReaderAsync(colorSource);
                Log($"BGRA8 reader unavailable ({ex.GetType().Name}); using native format.");
            }

            _reader.FrameArrived += OnFrameArrived;
            var status = await _reader.StartAsync();
            Log($"Reader start status: {status}");

            IsRunning = status is MediaFrameReaderStartStatus.Success;
            return IsRunning;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or COMException)
        {
            // No camera, camera in use, or privacy settings block desktop-app access.
            Log($"StartAsync failed: {ex.GetType().Name}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Points the source at an uncompressed format so frames decode to a SoftwareBitmap cheaply.
    /// Prefers a sensible resolution (largest at or below 1080p). Best-effort: errors are ignored.
    /// </summary>
    private async Task TrySelectUncompressedFormat(MediaFrameSource source)
    {
        try
        {
            MediaFrameFormat? best = null;
            int bestRank = int.MaxValue;
            long bestPixels = 0;

            foreach (var format in source.SupportedFormats)
            {
                if (format.VideoFormat is null)
                    continue;

                int rank = Array.IndexOf(PreferredSubtypes, (format.Subtype ?? "").ToUpperInvariant());
                if (rank < 0)
                    continue; // compressed / unknown subtype

                long pixels = (long)format.VideoFormat.Width * format.VideoFormat.Height;
                if (pixels > 1280L * 720L)
                    continue; // 720p is plenty for preview + print, and keeps the copy cheap

                bool better = rank < bestRank || (rank == bestRank && pixels > bestPixels);
                if (better)
                {
                    best = format;
                    bestRank = rank;
                    bestPixels = pixels;
                }
            }

            if (best is not null)
            {
                await source.SetFormatAsync(best);
                Log($"Source format set to {best.Subtype} {best.VideoFormat.Width}x{best.VideoFormat.Height}.");
            }
            else
            {
                Log("No uncompressed source format available; relying on reader transcode.");
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException)
        {
            Log($"SetFormat failed: {ex.GetType().Name}; continuing.");
        }
    }

    private static void Log(string message)
    {
        try
        {
            File.AppendAllText(LogPath, $"{DateTime.Now:HH:mm:ss.fff}  {message}{Environment.NewLine}");
        }
        catch
        {
            // Diagnostics must never affect runtime behaviour.
        }
    }

    private MediaFrameSourceGroup SelectGroup(IReadOnlyList<MediaFrameSourceGroup> groups)
    {
        if (!string.IsNullOrWhiteSpace(_preferredCamera))
        {
            var match = groups.FirstOrDefault(g =>
                g.DisplayName.Contains(_preferredCamera, StringComparison.OrdinalIgnoreCase));
            if (match is not null)
                return match;
        }

        // Prefer a group that actually exposes a colour camera.
        return groups.FirstOrDefault(g =>
                   g.SourceInfos.Any(i => i.SourceKind == MediaFrameSourceKind.Color))
               ?? groups[0];
    }

    private void OnFrameArrived(MediaFrameReader sender, MediaFrameArrivedEventArgs args)
    {
        using var frame = sender.TryAcquireLatestFrame();
        var softwareBitmap = frame?.VideoMediaFrame?.SoftwareBitmap;
        if (softwareBitmap is null)
        {
            if (!_firstFrameLogged)
            {
                _firstFrameLogged = true;
                Log($"First frame: SoftwareBitmap is null (frame={(frame is null ? "null" : "present")}).");
            }
            return;
        }

        if (!_firstFrameLogged)
        {
            _firstFrameLogged = true;
            Log($"First frame OK: {softwareBitmap.BitmapPixelFormat} " +
                $"{softwareBitmap.PixelWidth}x{softwareBitmap.PixelHeight}.");
        }

        Bitmap bitmap;
        try
        {
            bitmap = SoftwareBitmapConverter.ToBitmap(softwareBitmap);
        }
        catch (Exception ex)
        {
            if (!_conversionErrorLogged)
            {
                _conversionErrorLogged = true; // log once, don't spam per frame
                Log($"Frame conversion failed: {ex.GetType().Name}: {ex.Message}");
            }
            return; // A single bad frame should never tear down the stream.
        }

        lock (_sync)
        {
            if (_disposed)
            {
                bitmap.Dispose();
                return;
            }
            _latest?.Dispose();
            _latest = bitmap;
        }

        FrameReady?.Invoke(this, EventArgs.Empty);
    }

    public Bitmap? GetFrameCopy()
    {
        lock (_sync)
        {
            return _latest is null ? null : new Bitmap(_latest);
        }
    }

    public void Dispose()
    {
        lock (_sync)
        {
            if (_disposed)
                return;
            _disposed = true;
        }

        IsRunning = false;

        if (_reader is not null)
        {
            _reader.FrameArrived -= OnFrameArrived;
            try { _reader.StopAsync().AsTask().GetAwaiter().GetResult(); } catch { /* shutting down */ }
            _reader.Dispose();
            _reader = null;
        }

        _mediaCapture?.Dispose();
        _mediaCapture = null;

        lock (_sync)
        {
            _latest?.Dispose();
            _latest = null;
        }
    }
}
