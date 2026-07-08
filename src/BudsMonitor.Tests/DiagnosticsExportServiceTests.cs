using System.IO.Compression;
using System.Text;
using BudsMonitor.Diagnostics;
using BudsMonitor.Infrastructure.Cache;
using BudsMonitor.Infrastructure.Devices;
using BudsMonitor.Infrastructure.Settings;
using BudsMonitor.Infrastructure.Storage;

namespace BudsMonitor.Tests;

public sealed class DiagnosticsExportServiceTests : IDisposable
{
    private readonly string _root;
    private readonly StoragePaths _paths;

    public DiagnosticsExportServiceTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "budsmon-diag-" + Guid.NewGuid().ToString("N"));
        _paths = new StoragePaths(Path.Combine(_root, "roaming"), Path.Combine(_root, "local"));
        Directory.CreateDirectory(_paths.LogsDirectory);
        File.WriteAllText(Path.Combine(_paths.LogsDirectory, "app-20260101.log"), "sample log line\n");
    }

    private static DiagnosticsInput BuildInput(DateTimeOffset now, bool mask, bool includeRaw) => new()
    {
        GeneratedAt = now,
        MaskBluetoothAddresses = mask,
        IncludeRawPayloads = includeRaw,
        AppVersion = "1.2.3",
        Settings = new BudsMonitorSettings(),
        Devices = new DeviceRegistryFile(),
        BatteryCache = new BatteryCacheFile(),
        Scanner = new DiagnosticsScannerStatus { State = "Running", TotalFramesReceived = 42 },
        AdvertisementSamples =
        [
            new DiagnosticsAdvertisementSample
            {
                ReceivedAt = now,
                CompanyId = 0x004C,
                BluetoothAddress = 0xAABBCCDDEEFF,
                DataLength = 11,
                Rssi = -60,
                LocalName = "AirPods",
                ManufacturerData = [0x07, 0x19, 0x01, 0x0A],
            },
        ],
    };

    private static string ReadEntry(string zipPath, string entryName)
    {
        using var archive = ZipFile.OpenRead(zipPath);
        var entry = archive.GetEntry(entryName)
                    ?? throw new Xunit.Sdk.XunitException($"missing entry: {entryName}");
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    [Fact]
    public void Export_creates_zip_with_expected_entries()
    {
        var zip = new DiagnosticsExportService(_paths).Export(
            BuildInput(DateTimeOffset.Now, mask: true, includeRaw: false));

        Assert.True(File.Exists(zip));
        Assert.StartsWith(_paths.DiagnosticsDirectory, zip);

        using var archive = ZipFile.OpenRead(zip);
        var names = archive.Entries.Select(e => e.FullName).ToHashSet();
        Assert.Contains("environment.json", names);
        Assert.Contains("settings.json", names);
        Assert.Contains("devices.json", names);
        Assert.Contains("battery-cache.json", names);
        Assert.Contains("scanner.json", names);
        Assert.Contains("provider-attempts.json", names);
        Assert.Contains("advertisement-samples.json", names);
        Assert.Contains("README.txt", names);
        Assert.Contains("logs/app-20260101.log", names);
    }

    [Fact]
    public void Export_masks_bluetooth_address_by_default()
    {
        var zip = new DiagnosticsExportService(_paths).Export(
            BuildInput(DateTimeOffset.Now, mask: true, includeRaw: false));

        var samples = ReadEntry(zip, "advertisement-samples.json");
        Assert.Contains("AA:BB:CC:**:**:**", samples);
        Assert.DoesNotContain("AA:BB:CC:DD:EE:FF", samples);
        // Raw payload must be absent unless opted in.
        Assert.DoesNotContain("manufacturerDataHex\": \"07", samples);
    }

    [Fact]
    public void Export_includes_full_address_and_raw_payload_when_opted_in()
    {
        var zip = new DiagnosticsExportService(_paths).Export(
            BuildInput(DateTimeOffset.Now, mask: false, includeRaw: true));

        var samples = ReadEntry(zip, "advertisement-samples.json");
        Assert.Contains("AA:BB:CC:DD:EE:FF", samples);
        Assert.Contains("0719010A", samples);
    }

    [Fact]
    public void Export_environment_omits_machine_and_user_name()
    {
        var zip = new DiagnosticsExportService(_paths).Export(
            BuildInput(DateTimeOffset.Now, mask: true, includeRaw: false));

        var environment = ReadEntry(zip, "environment.json");
        Assert.DoesNotContain(Environment.MachineName, environment);
        Assert.DoesNotContain(Environment.UserName, environment);
        Assert.Contains("1.2.3", environment);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort temp cleanup.
        }
    }
}
