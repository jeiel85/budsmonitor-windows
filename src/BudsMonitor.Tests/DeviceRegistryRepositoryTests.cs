using BudsMonitor.Infrastructure.Devices;

namespace BudsMonitor.Tests;

public sealed class DeviceRegistryRepositoryTests : IDisposable
{
    private readonly string _dir;
    private readonly string _file;

    public DeviceRegistryRepositoryTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "BudsMonitorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_dir);
        _file = Path.Combine(_dir, "devices.json");
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir))
        {
            Directory.Delete(_dir, recursive: true);
        }
    }

    [Fact]
    public void Load_WhenMissing_ReturnsEmpty()
    {
        var repository = new DeviceRegistryRepository(_file);
        Assert.Empty(repository.Load().Devices);
    }

    [Fact]
    public void SaveThenLoad_RoundTripsPreferences()
    {
        var repository = new DeviceRegistryRepository(_file);
        var registry = new DeviceRegistryFile
        {
            Devices =
            [
                new DeviceRegistryEntry
                {
                    StableDeviceKey = "sha256:a",
                    DisplayName = "AirPods Pro 2",
                    Alias = "내 에어팟",
                    ProviderHint = "airpods-ble-advertisement",
                    IsPinned = true,
                    IsHidden = false,
                    FirstSeenAt = new DateTimeOffset(2026, 7, 8, 12, 0, 0, TimeSpan.Zero),
                    LastSeenAt = new DateTimeOffset(2026, 7, 8, 13, 0, 0, TimeSpan.Zero),
                },
            ],
        };

        repository.Save(registry);
        var loaded = repository.Load();

        var entry = Assert.Single(loaded.Devices);
        Assert.Equal("내 에어팟", entry.Alias);
        Assert.True(entry.IsPinned);
        Assert.False(entry.IsHidden);
        Assert.Equal("airpods-ble-advertisement", entry.ProviderHint);
    }

    [Fact]
    public void Load_WhenCorrupt_ReturnsEmpty()
    {
        File.WriteAllText(_file, "not json");
        Assert.Empty(new DeviceRegistryRepository(_file).Load().Devices);
    }
}
