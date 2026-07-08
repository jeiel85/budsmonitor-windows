using System.Security.Cryptography;
using System.Text;
using BudsMonitor.Domain;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace BudsMonitor.Bluetooth;

/// <summary>
/// Enumerates paired BLE devices as <see cref="DeviceCandidate"/>s. Kept separate from the
/// GATT provider so the provider only answers "can I read battery for this candidate?".
/// </summary>
public sealed class PairedBleDeviceEnumerator
{
    public async Task<IReadOnlyList<DeviceCandidate>> GetPairedDevicesAsync(CancellationToken cancellationToken)
    {
        var selector = BluetoothLEDevice.GetDeviceSelectorFromPairingState(true);
        var devices = await DeviceInformation.FindAllAsync(selector).AsTask(cancellationToken);

        var candidates = new List<DeviceCandidate>(devices.Count);
        foreach (var info in devices)
        {
            candidates.Add(new DeviceCandidate
            {
                StableDeviceKey = BuildStableKey(info.Id),
                DisplayName = string.IsNullOrWhiteSpace(info.Name) ? "알 수 없는 기기" : info.Name,
                Kind = DeviceKind.Unknown,
                WindowsDeviceId = info.Id,
            });
        }

        return candidates;
    }

    private static string BuildStableKey(string deviceId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(deviceId));
        return "sha256:" + Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
