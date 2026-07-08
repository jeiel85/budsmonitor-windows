using BudsMonitor.Infrastructure.Cache;

namespace BudsMonitor.Tests;

public sealed class BatteryCacheRepositoryTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public BatteryCacheRepositoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "BudsMonitorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "battery-cache.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenMissing_ReturnsEmptyCache()
    {
        var repo = new BatteryCacheRepository(_file);

        var cache = repo.Load();

        Assert.Equal(1, cache.Version);
        Assert.Empty(cache.Snapshots);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsSnapshots()
    {
        var repo = new BatteryCacheRepository(_file);
        var cache = new BatteryCacheFile
        {
            Snapshots =
            [
                new CachedBatterySnapshot
                {
                    StableDeviceKey = "sha256:abc",
                    DisplayName = "AirPods Pro 2",
                    Source = "AirPodsBleAdvertisement",
                    MeasuredAt = new DateTimeOffset(2026, 7, 8, 13, 42, 0, TimeSpan.FromHours(9)),
                    ExpectedFreshnessSeconds = 30,
                    Components =
                    [
                        new CachedBatteryComponent { Type = "LeftBud", Percentage = 80, IsCharging = false },
                        new CachedBatteryComponent { Type = "Case", Percentage = 60, IsCharging = true },
                    ],
                },
            ],
        };

        repo.Save(cache);
        var loaded = repo.Load();

        var snapshot = Assert.Single(loaded.Snapshots);
        Assert.Equal("AirPods Pro 2", snapshot.DisplayName);
        Assert.Equal("AirPodsBleAdvertisement", snapshot.Source);
        Assert.Equal(30, snapshot.ExpectedFreshnessSeconds);
        Assert.Equal(2, snapshot.Components.Count);
        Assert.Equal("LeftBud", snapshot.Components[0].Type);
        Assert.Equal(80, snapshot.Components[0].Percentage);
        Assert.True(snapshot.Components[1].IsCharging);
    }

    [Fact]
    public void Load_WhenCorrupt_ReturnsEmptyCache()
    {
        File.WriteAllText(_file, "not json at all");
        var repo = new BatteryCacheRepository(_file);

        var cache = repo.Load();

        Assert.Empty(cache.Snapshots);
    }
}
