using System.Security.Cryptography;
using System.Text;
using BudsMonitor.Domain;

namespace BudsMonitor.Providers.GalaxyBuds;

/// <summary>
/// Recognizes Galaxy Buds from BLE advertisements and surfaces them as a limited-support
/// device. Galaxy Buds report battery over a proprietary, encrypted RFCOMM protocol — not in
/// BLE advertisements or standard GATT — so no battery values are produced here. The snapshot
/// exists so the device is visible on the dashboard and captured in diagnostics bundles.
/// </summary>
public sealed class GalaxyBudsAdvertisementProvider : IAdvertisementBatteryProvider
{
    public const string LimitedSupportDiagnosticCode = "galaxy-buds-limited-support";

    private readonly GalaxyBudsClassifier _classifier;

    public GalaxyBudsAdvertisementProvider(GalaxyBudsClassifier? classifier = null)
        => _classifier = classifier ?? new GalaxyBudsClassifier();

    public string ProviderId => "galaxy-buds";

    public bool TryParseAdvertisement(BleAdvertisementFrame frame, out BatteryReadResult result)
    {
        var match = _classifier.Classify(frame.LocalName);
        if (match is null)
        {
            result = BatteryReadResult.NotApplicable(ProviderId);
            return false;
        }

        var key = BuildStableKey(match.Model, frame.BluetoothAddress);
        result = BatteryReadResult.Success(
            ProviderId,
            CreateLimitedSupportSnapshot(key, match.DisplayName, match.DisplayName, frame.ReceivedAt));
        return true;
    }

    /// <summary>
    /// Builds a battery-less snapshot marking the device as recognized-but-limited. Shared by the
    /// advertisement path and the GATT-fallback path (connected buds with no standard battery).
    /// </summary>
    public static BatterySnapshot CreateLimitedSupportSnapshot(
        string stableDeviceKey, string displayName, string modelName, DateTimeOffset measuredAt) => new()
    {
        StableDeviceKey = stableDeviceKey,
        DisplayName = displayName,
        DeviceKind = DeviceKind.Earbuds,
        Components = [],
        Source = BatteryDataSource.GalaxyBudsProvider,
        Confidence = BatteryConfidence.Low,
        MeasuredAt = measuredAt,
        ExpectedFreshness = TimeSpan.FromSeconds(30),
        ModelName = modelName,
        ProviderDiagnosticCode = LimitedSupportDiagnosticCode,
    };

    private static string BuildStableKey(string model, ulong bluetoothAddress)
    {
        var raw = $"galaxy-buds:{model}:{bluetoothAddress:X12}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return "sha256:" + Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
