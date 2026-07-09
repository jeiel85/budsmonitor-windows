using BudsMonitor.Application.Devices;
using BudsMonitor.Domain;

namespace BudsMonitor.Tests;

public sealed class EarbudFamilyTests
{
    [Theory]
    [InlineData("AirPods 3", "airpods")]
    [InlineData("AirPods 2", "airpods")]
    [InlineData("용은의 AirPods", "airpods")]
    [InlineData("AirPods Pro 2 USB-C", "airpods-pro")]
    [InlineData("용은의 AirPods Pro - Find My", "airpods-pro")]
    [InlineData("AirPods Max USB-C", "airpods-max")]
    [InlineData("Galaxy Buds2 Pro", "galaxy-buds")]
    public void Maps_known_families(string name, string expected)
        => Assert.Equal(expected, EarbudFamily.Of(name));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("용은의 S24")]
    [InlineData("MX Master 3")]
    public void Returns_null_for_non_earbuds(string? name)
        => Assert.Null(EarbudFamily.Of(name));
}

public sealed class DeviceListResolverTests
{
    private static BatterySnapshot Snap(
        string key, string display, BatteryDataSource source, int? rssi, DateTimeOffset measured)
        => new()
        {
            StableDeviceKey = key,
            DisplayName = display,
            DeviceKind = DeviceKind.Earbuds,
            Components = [],
            Source = source,
            Confidence = BatteryConfidence.High,
            MeasuredAt = measured,
            ModelName = display,
            Rssi = rssi,
        };

    private static readonly DateTimeOffset T0 = new(2026, 7, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Gatt_device_passes_through_with_its_own_key()
    {
        var r = new DeviceListResolver();
        var s = Snap("sha256:mouse", "MX Master 3", BatteryDataSource.StandardGattBatteryService, null, T0);
        Assert.Equal("sha256:mouse", r.Resolve(s, T0));
    }

    [Fact]
    public void Earbud_collapses_to_one_key_per_family()
    {
        var r = new DeviceListResolver { ShowPairedOnly = false };
        var a = Snap("sha256:addr1", "AirPods 3", BatteryDataSource.AirPodsBleAdvertisement, -50, T0);
        var b = Snap("sha256:addr2", "AirPods 3", BatteryDataSource.AirPodsBleAdvertisement, -50, T0.AddMinutes(1));
        Assert.Equal("family:airpods", r.Resolve(a, T0));
        Assert.Equal("family:airpods", r.Resolve(b, T0.AddMinutes(1)));
    }

    [Fact]
    public void Filters_to_paired_families()
    {
        var r = new DeviceListResolver { ShowPairedOnly = true, PairedFamilies = new HashSet<string> { "airpods" } };
        var pro = Snap("k", "AirPods Pro 2 USB-C", BatteryDataSource.AirPodsBleAdvertisement, -50, T0);
        var reg = Snap("k2", "AirPods 3", BatteryDataSource.AirPodsBleAdvertisement, -50, T0);
        Assert.Null(r.Resolve(pro, T0));                       // not a paired family
        Assert.Equal("family:airpods", r.Resolve(reg, T0));    // paired family
    }

    [Fact]
    public void Empty_paired_families_falls_back_to_showing_all()
    {
        // Enumeration found nothing (or hasn't run yet) — don't hide everything.
        var r = new DeviceListResolver { ShowPairedOnly = true, PairedFamilies = new HashSet<string>() };
        var s = Snap("k", "AirPods Pro 2 USB-C", BatteryDataSource.AirPodsBleAdvertisement, -50, T0);
        Assert.Equal("family:airpods-pro", r.Resolve(s, T0));
    }

    [Fact]
    public void Prefers_stronger_signal_within_the_fresh_window()
    {
        var r = new DeviceListResolver { ShowPairedOnly = false };
        var mine = Snap("k1", "AirPods Pro 2 USB-C", BatteryDataSource.AirPodsBleAdvertisement, -45, T0);
        var distant = Snap("k2", "AirPods Pro 2 USB-C", BatteryDataSource.AirPodsBleAdvertisement, -85, T0.AddSeconds(2));

        Assert.Equal("family:airpods-pro", r.Resolve(mine, T0));     // nearer holds the card
        Assert.Null(r.Resolve(distant, T0.AddSeconds(2)));           // farther is dropped
    }

    [Fact]
    public void Stale_best_is_replaced_by_a_later_reading()
    {
        var r = new DeviceListResolver { ShowPairedOnly = false };
        var strong = Snap("k1", "AirPods 3", BatteryDataSource.AirPodsBleAdvertisement, -40, T0);
        var weakLater = Snap("k2", "AirPods 3", BatteryDataSource.AirPodsBleAdvertisement, -80, T0.AddSeconds(45));

        Assert.Equal("family:airpods", r.Resolve(strong, T0));
        // 45s later the strong reading is stale, so a weaker one may take the card.
        Assert.Equal("family:airpods", r.Resolve(weakLater, T0.AddSeconds(45)));
    }
}
