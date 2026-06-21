using System.Drawing;
using System.Drawing.Imaging;
using MonoBooth.Camera;
using MonoBooth.Configuration;
using MonoBooth.Imaging;
using MonoBooth.Printing;

namespace MonoBooth.Session;

/// <summary>
/// Drives a single booth run: countdown, capture N frames, compose the strip, save and print.
/// Written as one linear <c>async</c> method instead of the old timer/counter state machine, so the
/// flow reads top-to-bottom and the UI thread stays responsive between steps.
/// </summary>
public sealed class PhotoBoothSession
{
    private readonly ICameraService _camera;
    private readonly AppSettings _settings;

    public PhotoBoothSession(ICameraService camera, AppSettings settings)
    {
        _camera = camera;
        _settings = settings;
    }

    public bool IsRunning { get; private set; }

    /// <summary>Big centred message: "Get Ready", "3", "2", "1", "Smile!", or "" to clear.</summary>
    public event Action<string>? StatusChanged;

    /// <summary>Fired at the instant of capture so the UI can flash the screen.</summary>
    public event Action? FlashRequested;

    /// <summary>Fired after each shot with its index and a display copy (UI owns/disposes it).</summary>
    public event Action<int, Bitmap>? FrameCaptured;

    /// <summary>Fired once the strip is composed, saved and (optionally) printed.</summary>
    public event Action<SessionResult>? Completed;

    /// <summary>Fired if the run can't complete (e.g. camera produced no frame).</summary>
    public event Action<string>? Failed;

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        if (IsRunning)
            return;

        IsRunning = true;
        var frames = new List<Bitmap>();

        try
        {
            StatusChanged?.Invoke("Get Ready");
            await Task.Delay(1200, cancellationToken);

            for (int i = 0; i < _settings.FrameCount; i++)
            {
                for (int n = _settings.CountdownSeconds; n >= 1; n--)
                {
                    StatusChanged?.Invoke(n.ToString());
                    await Task.Delay(1000, cancellationToken);
                }

                StatusChanged?.Invoke("Smile!");
                await Task.Delay(450, cancellationToken);

                FlashRequested?.Invoke();
                var shot = _camera.GetFrameCopy();
                StatusChanged?.Invoke("");

                if (shot is null)
                {
                    Failed?.Invoke("The camera didn't produce a frame. Is it connected?");
                    return;
                }

                frames.Add(shot);
                FrameCaptured?.Invoke(i, new Bitmap(shot));

                await Task.Delay(_settings.ReviewMilliseconds, cancellationToken);
            }

            StatusChanged?.Invoke("Say cheese!");

            var result = await Task.Run(() => FinishStrip(frames), cancellationToken);
            StatusChanged?.Invoke("");
            Completed?.Invoke(result);
        }
        catch (OperationCanceledException)
        {
            StatusChanged?.Invoke("");
        }
        finally
        {
            foreach (var frame in frames)
                frame.Dispose();
            IsRunning = false;
        }
    }

    private SessionResult FinishStrip(List<Bitmap> frames)
    {
        using var strip = FilmstripComposer.Compose(frames, _settings.BorderWidth, ParseColor(_settings.BorderColor));

        var folder = EnsureSessionFolder(out var timestamp);

        // Save the individual frames alongside the strip for keepsakes / reprints.
        for (int i = 0; i < frames.Count; i++)
            frames[i].Save(Path.Combine(folder, $"{timestamp}-{i + 1}.jpg"), ImageFormat.Jpeg);

        var stripPath = Path.Combine(folder, $"{timestamp}-strip.jpg");
        strip.Save(stripPath, ImageFormat.Jpeg);

        bool printed = _settings.PrintEnabled && FilmstripPrinter.Print(strip, _settings.PrintCopies);

        return new SessionResult(stripPath, new Bitmap(strip), printed);
    }

    private string EnsureSessionFolder(out string timestamp)
    {
        timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var folder = _settings.ResolvedOutputDirectory;
        Directory.CreateDirectory(folder);
        return folder;
    }

    /// <summary>Accepts both named colours ("Black") and hex ("#1a1a1a"); falls back to black.</summary>
    private static Color ParseColor(string name)
    {
        try
        {
            return ColorTranslator.FromHtml(string.IsNullOrWhiteSpace(name) ? "Black" : name);
        }
        catch (Exception)
        {
            return Color.Black;
        }
    }
}
