using System.Drawing;
using System.Runtime.InteropServices;
using Windows.Media.Capture;
using Windows.Media.Capture.Frames;

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
                return false;

            DeviceName = group.DisplayName;

            _reader = await _mediaCapture.CreateFrameReaderAsync(colorSource);
            _reader.FrameArrived += OnFrameArrived;
            var status = await _reader.StartAsync();

            IsRunning = status is MediaFrameReaderStartStatus.Success;
            return IsRunning;
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or InvalidOperationException or COMException)
        {
            // No camera, camera in use, or privacy settings block desktop-app access.
            return false;
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
            return;

        Bitmap bitmap;
        try
        {
            bitmap = SoftwareBitmapConverter.ToBitmap(softwareBitmap);
        }
        catch
        {
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
