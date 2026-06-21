using System.Drawing;

namespace MonoBooth.Camera;

/// <summary>
/// Abstraction over the live camera. Keeps the rest of the app free of WinRT details and makes
/// the booth testable with a fake camera if we ever want one.
/// </summary>
public interface ICameraService : IDisposable
{
    /// <summary>Raised (on a background thread) whenever a fresh preview frame is available.</summary>
    event EventHandler? FrameReady;

    /// <summary>Human-readable name of the active camera, once started.</summary>
    string? DeviceName { get; }

    bool IsRunning { get; }

    /// <summary>
    /// Starts streaming. Returns <c>false</c> (rather than throwing) when no camera is present or
    /// access is denied, so the UI can show a friendly message instead of crashing.
    /// </summary>
    Task<bool> StartAsync();

    /// <summary>
    /// Returns a fresh copy of the most recent frame, or <c>null</c> if none has arrived yet.
    /// The caller owns the returned <see cref="Bitmap"/> and must dispose it.
    /// </summary>
    Bitmap? GetFrameCopy();

    /// <summary>
    /// Draws the most recent frame, aspect-fit and centred, into <paramref name="bounds"/> without
    /// allocating a copy. Returns <c>false</c> if no frame has arrived yet. Intended to be called
    /// from a paint handler driven by a steady render timer.
    /// </summary>
    bool TryRenderLatest(Graphics graphics, Rectangle bounds);
}
