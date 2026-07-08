using BudsMonitor.Infrastructure.Settings;

namespace BudsMonitor.Tests;

public sealed class SettingsRepositoryTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public SettingsRepositoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "BudsMonitorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "settings.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void LoadOrCreate_WhenMissing_CreatesFileWithDefaults()
    {
        var repo = new SettingsRepository(_file);

        var settings = repo.LoadOrCreate();

        Assert.True(File.Exists(_file));
        Assert.Equal(1, settings.Version);
        Assert.True(settings.App.MinimizeToTray);
        Assert.Equal("system", settings.App.Theme);
        Assert.Equal(20, settings.Notifications.EarbudLowBatteryThreshold);
        Assert.Equal(120, settings.Monitoring.StaleAfterSeconds);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsValues()
    {
        var repo = new SettingsRepository(_file);
        var original = new BudsMonitorSettings
        {
            App = new AppSettings { Theme = "dark", MinimizeToTray = false },
        };

        repo.Save(original);
        var loaded = repo.LoadOrCreate();

        Assert.Equal("dark", loaded.App.Theme);
        Assert.False(loaded.App.MinimizeToTray);
    }

    [Fact]
    public void Load_ForcesLocalOnlyPrivacyRules()
    {
        // A tampered file that tries to enable analytics/network must be forced off on load.
        File.WriteAllText(_file,
            "{\"version\":1,\"privacy\":{\"analyticsEnabled\":true,\"networkEnabled\":true}}");
        var repo = new SettingsRepository(_file);

        var settings = repo.LoadOrCreate();

        Assert.False(settings.Privacy.AnalyticsEnabled);
        Assert.False(settings.Privacy.NetworkEnabled);
    }

    [Fact]
    public void LoadOrCreate_WhenCorrupt_FallsBackToDefaults()
    {
        File.WriteAllText(_file, "{ this is not valid json");
        var repo = new SettingsRepository(_file);

        var settings = repo.LoadOrCreate();

        Assert.Equal(1, settings.Version);
        Assert.Equal("system", settings.App.Theme);
    }
}
