using System.Drawing;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MonoBooth.Configuration;

/// <summary>
/// A rectangle expressed as percentages (0–100) of the window's client area, so positions scale
/// across screen resolutions. Used to place the preview and the photo strip over the background art.
/// </summary>
public sealed class LayoutArea
{
    public float X { get; set; }
    public float Y { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }

    public Rectangle ToPixels(Size client) => new(
        (int)Math.Round(X / 100f * client.Width),
        (int)Math.Round(Y / 100f * client.Height),
        Math.Max(1, (int)Math.Round(Width / 100f * client.Width)),
        Math.Max(1, (int)Math.Round(Height / 100f * client.Height)));

    public void SetFromPixels(Rectangle bounds, Size client)
    {
        if (client.Width <= 0 || client.Height <= 0)
            return;
        X = bounds.X * 100f / client.Width;
        Y = bounds.Y * 100f / client.Height;
        Width = bounds.Width * 100f / client.Width;
        Height = bounds.Height * 100f / client.Height;
    }
}

/// <summary>
/// All user-tweakable knobs for the booth. Persisted as <c>settings.json</c> next to the
/// executable. Anything not present in the file falls back to the defaults defined here, so a
/// missing or partial file is always safe.
/// </summary>
public sealed class AppSettings
{
    /// <summary>How many photos make up one filmstrip.</summary>
    public int FrameCount { get; set; } = 4;

    /// <summary>Seconds counted down before each shot ("3, 2, 1, smile").</summary>
    public int CountdownSeconds { get; set; } = 3;

    /// <summary>How long the captured frame is shown before moving on, in milliseconds.</summary>
    public int ReviewMilliseconds { get; set; } = 1200;

    /// <summary>Black border drawn around each photo in the filmstrip, in pixels.</summary>
    public int BorderWidth { get; set; } = 12;

    /// <summary>Border colour name (any <see cref="System.Drawing.Color"/> name).</summary>
    public string BorderColor { get; set; } = "Black";

    /// <summary>Run the window borderless and full-screen (kiosk mode).</summary>
    public bool FullScreen { get; set; } = true;

    /// <summary>Optional path to a background image. Empty => use the bundled background.</summary>
    public string BackgroundImagePath { get; set; } = "";

    /// <summary>
    /// Where the live preview sits over the background (percent of the window). Kept deliberately
    /// smaller than the screen so a decorative/event background frames it. Right-drag to reposition.
    /// </summary>
    public LayoutArea PreviewArea { get; set; } = new() { X = 6, Y = 9, Width = 50, Height = 60 };

    /// <summary>Where the photo strip sits over the background (percent of the window).</summary>
    public LayoutArea StripArea { get; set; } = new() { X = 62, Y = 9, Width = 20, Height = 72 };

    /// <summary>
    /// Where finished filmstrips and individual frames are written. Supports the token
    /// <c>{Pictures}</c> which expands to the user's Pictures folder.
    /// </summary>
    public string OutputDirectory { get; set; } = "{Pictures}/MonoBooth";

    /// <summary>Preferred camera by name substring (e.g. "Logitech"). Empty => first camera.</summary>
    public string PreferredCamera { get; set; } = "";

    /// <summary>Send the finished filmstrip to the default printer.</summary>
    public bool PrintEnabled { get; set; } = true;

    /// <summary>How many copies of the strip to lay out on the page (side by side).</summary>
    public int PrintCopies { get; set; } = 2;

    [JsonIgnore]
    public string ResolvedOutputDirectory =>
        OutputDirectory.Replace(
            "{Pictures}",
            Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
            StringComparison.OrdinalIgnoreCase);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    /// <summary>
    /// Loads settings from <paramref name="path"/>. If the file is missing it is created with
    /// defaults; if it is present but corrupt the defaults are returned without overwriting it.
    /// </summary>
    public static AppSettings Load(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                    return loaded;
            }
            else
            {
                var defaults = new AppSettings();
                defaults.Save(path);
                return defaults;
            }
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // A bad config file should never stop the booth from opening.
        }

        return new AppSettings();
    }

    public void Save(string path)
    {
        try
        {
            File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOptions));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Non-fatal: running from a read-only location just means settings aren't persisted.
        }
    }
}
