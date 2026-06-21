namespace MonoBooth.Configuration;

/// <summary>
/// Per-user writable locations for settings and logs. The app may be installed under Program Files
/// (read-only for standard users), so runtime state lives in %LOCALAPPDATA%\MonoBooth instead of
/// next to the executable.
/// </summary>
public static class AppPaths
{
    public static string DataDirectory { get; } = ResolveDataDirectory();

    public static string SettingsFile => Path.Combine(DataDirectory, "settings.json");

    public static string CameraLog => Path.Combine(DataDirectory, "monobooth-camera.log");

    private static string ResolveDataDirectory()
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MonoBooth");
            Directory.CreateDirectory(dir);
            return dir;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Last resort: sit next to the exe (works when run from a writable folder).
            return AppContext.BaseDirectory;
        }
    }
}
