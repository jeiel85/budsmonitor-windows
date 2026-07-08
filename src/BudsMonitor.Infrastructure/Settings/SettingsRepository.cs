using System.Text.Json;
using BudsMonitor.Infrastructure.Json;
using BudsMonitor.Infrastructure.Storage;

namespace BudsMonitor.Infrastructure.Settings;

/// <summary>
/// Loads and saves <see cref="BudsMonitorSettings"/> as JSON. Creates the file with
/// defaults on first run and falls back to defaults when the file is corrupt.
/// </summary>
public sealed class SettingsRepository
{
    private readonly string _filePath;

    public SettingsRepository(StoragePaths paths)
        : this(paths.SettingsFile)
    {
    }

    public SettingsRepository(string filePath)
    {
        _filePath = filePath;
    }

    /// <summary>
    /// Returns the persisted settings, creating the file with defaults if it does not
    /// exist. A corrupt file falls back to defaults and is left in place for inspection.
    /// </summary>
    public BudsMonitorSettings LoadOrCreate()
    {
        if (!File.Exists(_filePath))
        {
            var defaults = new BudsMonitorSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var settings = JsonSerializer.Deserialize<BudsMonitorSettings>(json, StorageJson.Options);
            return settings is null ? new BudsMonitorSettings() : Sanitize(settings);
        }
        catch (JsonException)
        {
            return new BudsMonitorSettings();
        }
    }

    public void Save(BudsMonitorSettings settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
        var json = JsonSerializer.Serialize(Sanitize(settings), StorageJson.Options);
        File.WriteAllText(_filePath, json);
    }

    /// <summary>Enforces the local-only hard rules regardless of file contents.</summary>
    private static BudsMonitorSettings Sanitize(BudsMonitorSettings settings) =>
        settings with
        {
            Privacy = settings.Privacy with
            {
                AnalyticsEnabled = false,
                NetworkEnabled = false,
            },
        };
}
