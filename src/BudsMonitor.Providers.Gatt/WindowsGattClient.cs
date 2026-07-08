using System.Runtime.InteropServices.WindowsRuntime;
using BudsMonitor.Domain;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;

namespace BudsMonitor.Providers.Gatt;

/// <summary>
/// Reads the Bluetooth SIG Battery Service (0x180F) / Battery Level (0x2A19) over WinRT GATT.
/// Absence of the service maps to NotApplicable; other conditions map to failure reasons.
/// </summary>
public sealed class WindowsGattClient : IGattClient
{
    public async Task<GattBatteryReadOutcome> ReadBatteryLevelAsync(string windowsDeviceId, CancellationToken cancellationToken)
    {
        BluetoothLEDevice? device = null;
        try
        {
            device = await BluetoothLEDevice.FromIdAsync(windowsDeviceId).AsTask(cancellationToken);
            if (device is null)
            {
                return GattBatteryReadOutcome.Failure(BatteryReadFailureReason.DeviceNotConnected);
            }

            var services = await device
                .GetGattServicesForUuidAsync(GattServiceUuids.Battery, BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);
            if (services.Status != GattCommunicationStatus.Success)
            {
                return GattBatteryReadOutcome.Failure(GattReadFailureMapper.FromGattStatus(services.Status));
            }
            if (services.Services.Count == 0)
            {
                return GattBatteryReadOutcome.NotApplicable();
            }

            var characteristics = await services.Services[0]
                .GetCharacteristicsForUuidAsync(GattCharacteristicUuids.BatteryLevel, BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);
            if (characteristics.Status != GattCommunicationStatus.Success)
            {
                return GattBatteryReadOutcome.Failure(GattReadFailureMapper.FromGattStatus(characteristics.Status));
            }
            if (characteristics.Characteristics.Count == 0)
            {
                return GattBatteryReadOutcome.Failure(BatteryReadFailureReason.BatteryCharacteristicNotFound);
            }

            var read = await characteristics.Characteristics[0]
                .ReadValueAsync(BluetoothCacheMode.Uncached)
                .AsTask(cancellationToken);
            if (read.Status != GattCommunicationStatus.Success)
            {
                return GattBatteryReadOutcome.Failure(GattReadFailureMapper.FromGattStatus(read.Status));
            }

            return BatteryLevelParser.TryParse(read.Value.ToArray(), out var percentage)
                ? GattBatteryReadOutcome.Success(percentage)
                : GattBatteryReadOutcome.Failure(BatteryReadFailureReason.InvalidBatteryPayload);
        }
        catch (Exception exception)
        {
            return GattBatteryReadOutcome.Failure(GattReadFailureMapper.FromException(exception));
        }
        finally
        {
            device?.Dispose();
        }
    }
}
