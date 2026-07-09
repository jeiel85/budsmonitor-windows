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

    /// <summary>
    /// Names of devices paired over Classic Bluetooth (e.g. "용은의 AirPods", "용은의 AirPods Pro").
    /// AirPods pair as Classic audio, so this is how we learn which earbud families are "yours".
    /// </summary>
    public async Task<IReadOnlyList<string>> GetPairedClassicNamesAsync(CancellationToken cancellationToken)
    {
        var selector = BluetoothDevice.GetDeviceSelectorFromPairingState(true);
        var devices = await DeviceInformation.FindAllAsync(selector).AsTask(cancellationToken);
        return devices
            .Select(info => info.Name)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .ToList();
    }

    private static string BuildStableKey(string deviceId)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(deviceId));
        return "sha256:" + Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }
}
