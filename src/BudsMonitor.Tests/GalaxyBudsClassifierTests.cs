using BudsMonitor.Domain;
using BudsMonitor.Providers.GalaxyBuds;

namespace BudsMonitor.Tests;

public sealed class GalaxyBudsClassifierTests
{
    private static readonly GalaxyBudsClassifier Classifier = new();

    [Fact]
    public void Embedded_profiles_load()
    {
        Assert.NotEmpty(GalaxyBudsProfileCatalog.Default.Profiles);
    }

    [Fact]
    public void Samsung_company_id_recognized()
    {
        Assert.True(GalaxyBudsClassifier.IsSamsungCompanyId(0x0075));
        Assert.False(GalaxyBudsClassifier.IsSamsungCompanyId(0x004C));
    }

    [Theory]
    [InlineData("Galaxy Buds2 Pro", "buds2pro")]
    [InlineData("Galaxy Buds3 Pro", "buds3pro")]
    [InlineData("Galaxy Buds Pro", "budspro")]
    [InlineData("Galaxy Buds Live", "budslive")]
    [InlineData("Galaxy Buds+", "budsplus")]
    [InlineData("Galaxy Buds2", "buds2")]
    [InlineData("Galaxy Buds", "buds")]
    public void Classify_picks_most_specific_model(string name, string expectedModel)
    {
        var match = Classifier.Classify(name);
        Assert.NotNull(match);
        Assert.Equal(expectedModel, match!.Model);
    }

    [Fact]
    public void Classify_matches_within_a_longer_device_name()
    {
        // Real devices prefix an owner name, e.g. "용은의 Galaxy Buds2 Pro".
        var match = Classifier.Classify("용은의 Galaxy Buds2 Pro");
        Assert.Equal("buds2pro", match?.Model);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Floor A/C")]          // real Samsung (0x0075) appliance from a captured bundle
    [InlineData("AirPods Pro")]
    [InlineData("Sony WF-1000XM5")]
    public void Classify_returns_null_for_non_galaxy_buds(string? name)
    {
        Assert.Null(Classifier.Classify(name));
    }

    private static BleAdvertisementFrame Frame(string? localName) => new()
    {
        ReceivedAt = DateTimeOffset.Now,
        CompanyId = GalaxyBudsClassifier.SamsungCompanyId,
        ManufacturerData = [0x01, 0x02, 0x03],
        BluetoothAddress = 0x0011_2233_4455,
        LocalName = localName,
    };

    [Fact]
    public void Provider_yields_limited_support_snapshot_for_galaxy_buds()
    {
        var provider = new GalaxyBudsAdvertisementProvider();
        var ok = provider.TryParseAdvertisement(Frame("Galaxy Buds2 Pro"), out var result);

        Assert.True(ok);
        Assert.Equal(BatteryReadStatus.Success, result.Status);
        Assert.NotNull(result.Snapshot);
        Assert.Empty(result.Snapshot!.Components);
        Assert.Equal(BatteryDataSource.GalaxyBudsProvider, result.Snapshot.Source);
        Assert.Equal(GalaxyBudsAdvertisementProvider.LimitedSupportDiagnosticCode,
            result.Snapshot.ProviderDiagnosticCode);
        Assert.Equal("Galaxy Buds2 Pro", result.Snapshot.DisplayName);
    }

    [Fact]
    public void Provider_is_not_applicable_for_other_devices()
    {
        var provider = new GalaxyBudsAdvertisementProvider();

        Assert.False(provider.TryParseAdvertisement(Frame("Floor A/C"), out var result));
        Assert.Equal(BatteryReadStatus.NotApplicable, result.Status);

        // Missing name must not throw.
        Assert.False(provider.TryParseAdvertisement(Frame(null), out _));
    }
}
