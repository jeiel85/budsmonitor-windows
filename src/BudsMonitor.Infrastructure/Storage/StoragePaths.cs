namespace BudsMonitor.Infrastructure.Storage;

/// <summary>
/// Resolves the on-disk locations BudsMonitor uses (see docs/09-storage-privacy).
/// Roaming configuration lives under %AppData%; machine-local cache and logs live
/// under %LocalAppData%. Roots are injectable so tests can redirect to a temp folder.
/// </summary>
public sealed class StoragePaths
{
    public StoragePaths(string appDataRoot, string localAppDataRoot)
    {
        AppDataRoot = appDataRoot;
        LocalAppDataRoot = localAppDataRoot;
    }

    public string AppDataRoot { get; }
    public string LocalAppDataRoot { get; }

    public string SettingsFile => Path.Combine(AppDataRoot, "settings.json");
    public string DevicesFile => Path.Combine(AppDataRoot, "devices.json");
    public string CacheDirectory => Path.Combine(LocalAppDataRoot, "cache");
    public string BatteryCacheFile => Path.Combine(CacheDirectory, "battery-cache.json");
    public string LogsDirectory => Path.Combine(LocalAppDataRoot, "logs");
    public string DiagnosticsDirectory => Path.Combine(LocalAppDataRoot, "diagnostics");

    /// <summary>Default locations under the current user's profile.</summary>
    public static StoragePaths CreateDefault()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return new StoragePaths(
            Path.Combine(appData, "BudsMonitor"),
            Path.Combine(localAppData, "BudsMonitor"));
    }
}
