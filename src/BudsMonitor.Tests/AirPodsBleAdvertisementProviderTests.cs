using BudsMonitor.Domain;
using BudsMonitor.Providers.AirPods;

namespace BudsMonitor.Tests;

public sealed class AirPodsBleAdvertisementProviderTests
{
    private static BleAdvertisementFrame Frame(ushort companyId, byte[] data) => new()
    {
        ReceivedAt = new DateTimeOffset(2026, 7, 8, 13, 42, 0, TimeSpan.Zero),
        CompanyId = companyId,
        ManufacturerData = data,
        BluetoothAddress = 0x708AD722E45D,
    };

    // AirPods Pro 2 Lightning, primaryLeft, L=80 R=90 Case=60 (charging), Playing Music.
    private static byte[] AirPodsPayload(byte podsBattery = 0x98, byte flagsAndCase = 0x46)
        => [0x07, 0x19, 0x01, 0x14, 0x20, 0x20, podsBattery, flagsAndCase, 0x00, 0x00, 0x05];

    [Fact]
    public void NonAppleFrame_ReturnsNotApplicable()
    {
        var provider = new AirPodsBleAdvertisementProvider();

        var ok = provider.TryParseAdvertisement(Frame(0x0006, AirPodsPayload()), out var result);

        Assert.False(ok);
        Assert.Equal(BatteryReadStatus.NotApplicable, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public void AppleNonAirPodsFrame_ReturnsNotApplicable()
    {
        var provider = new AirPodsBleAdvertisementProvider();
        // 0x10 Nearby type (not 0x07 proximity)
        var ok = provider.TryParseAdvertisement(Frame(0x004C, [0x10, 0x05, 0x00, 0x11, 0x22]), out var result);

        Assert.False(ok);
        Assert.Equal(BatteryReadStatus.NotApplicable, result.Status);
    }

    [Fact]
    public void AirPodsFrame_MapsToBatterySnapshot()
    {
        var provider = new AirPodsBleAdvertisementProvider();

        var ok = provider.TryParseAdvertisement(Frame(0x004C, AirPodsPayload()), out var result);

        Assert.True(ok);
        Assert.Equal(BatteryReadStatus.Success, result.Status);
        var snapshot = result.Snapshot!;
        Assert.Equal(BatteryDataSource.AirPodsBleAdvertisement, snapshot.Source);
        Assert.Equal(DeviceKind.Earbuds, snapshot.DeviceKind);
        Assert.Equal("AirPods Pro 2 Lightning", snapshot.ModelName);
        Assert.Equal(3, snapshot.Components.Count);
        Assert.Contains(snapshot.Components, c => c.Type == BatteryComponentType.LeftBud && c.Percentage == 80);
        Assert.Contains(snapshot.Components, c => c.Type == BatteryComponentType.RightBud && c.Percentage == 90);
        Assert.Contains(snapshot.Components, c => c.Type == BatteryComponentType.Case && c.Percentage == 60);
        Assert.StartsWith("sha256:", snapshot.StableDeviceKey);
        Assert.Equal(TimeSpan.FromSeconds(30), snapshot.ExpectedFreshness);
    }

    [Fact]
    public void UnavailableBattery_OmitsComponentInsteadOfZero()
    {
        var provider = new AirPodsBleAdvertisementProvider();
        // left nibble 0xF -> unavailable; right nibble 9 -> 90
        var ok = provider.TryParseAdvertisement(Frame(0x004C, AirPodsPayload(podsBattery: 0x9F)), out var result);

        Assert.True(ok);
        var snapshot = result.Snapshot!;
        Assert.DoesNotContain(snapshot.Components, c => c.Type == BatteryComponentType.LeftBud);
        Assert.Contains(snapshot.Components, c => c.Type == BatteryComponentType.RightBud && c.Percentage == 90);
    }
}
