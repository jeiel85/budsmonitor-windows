using System.Security.Cryptography;
using System.Text;
using BudsMonitor.Domain;

namespace BudsMonitor.Providers.AirPods;

/// <summary>
/// Turns Apple BLE proximity advertisement frames into a domain <see cref="BatterySnapshot"/>.
/// Only Company ID 0x004C frames that parse as an AirPods proximity payload are handled;
/// everything else returns NotApplicable so the resolver can try other providers.
/// </summary>
public sealed class AirPodsBleAdvertisementProvider : IAdvertisementBatteryProvider
{
    public const ushort AppleCompanyId = 0x004C;

    public string ProviderId => "airpods-ble-advertisement";

    public bool TryParseAdvertisement(BleAdvertisementFrame frame, out BatteryReadResult result)
    {
        if (frame.CompanyId != AppleCompanyId)
        {
            result = BatteryReadResult.NotApplicable(ProviderId);
            return false;
        }

        var context = new AirPodsAdvertisementContext
        {
            ReceivedAt = frame.ReceivedAt,
            BluetoothAddress = frame.BluetoothAddress,
            LocalName = frame.LocalName,
            RawRssi = frame.RawRssi,
        };

        if (!AirPodsAdvertisementParser.TryParse(frame.ManufacturerData, context, out var airpods))
        {
            // Apple frame but not an AirPods proximity payload (Nearby / Find My / etc.)
            result = BatteryReadResult.NotApplicable(ProviderId);
            return false;
        }

        result = BatteryReadResult.Success(ProviderId, ToBatterySnapshot(airpods, frame));
        return true;
    }

    private static BatterySnapshot ToBatterySnapshot(AirPodsAdvertisementSnapshot airpods, BleAdvertisementFrame frame)
    {
        var components = new List<BatteryComponent>();

        if (airpods.LeftBattery is int left)
        {
            components.Add(new BatteryComponent
            {
                Type = BatteryComponentType.LeftBud,
                Percentage = left,
                IsCharging = airpods.LeftCharging,
                Label = "Left",
            });
        }

        if (airpods.RightBattery is int right)
        {
            components.Add(new BatteryComponent
            {
                Type = BatteryComponentType.RightBud,
                Percentage = right,
                IsCharging = airpods.RightCharging,
                Label = "Right",
            });
        }

        if (airpods.CaseBattery is int caseBattery)
        {
            components.Add(new BatteryComponent
            {
                Type = BatteryComponentType.Case,
                Percentage = caseBattery,
                IsCharging = airpods.CaseCharging,
                Label = "Case",
            });
        }

        var displayName = airpods.ModelName == AirPodsModelCatalog.UnknownModelName
            ? "AirPods"
            : airpods.ModelName;

        return new BatterySnapshot
        {
            StableDeviceKey = BuildStableKey(airpods.ModelId, frame.BluetoothAddress),
            DisplayName = displayName,
            DeviceKind = DeviceKind.Earbuds,
            Components = components,
            Source = BatteryDataSource.AirPodsBleAdvertisement,
            Confidence = BatteryConfidence.High,
            MeasuredAt = airpods.ReceivedAt,
            ExpectedFreshness = TimeSpan.FromSeconds(30),
            ModelName = airpods.ModelName,
            Rssi = frame.RawRssi,
        };
    }

    private static string BuildStableKey(ushort modelId, ulong bluetoothAddress)
    {
        var raw = $"airpods-ble:{modelId:X4}:{bluetoothAddress:X12}";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return "sha256:" + Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
