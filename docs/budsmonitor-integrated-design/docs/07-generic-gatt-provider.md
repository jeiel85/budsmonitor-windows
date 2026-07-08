# 07. Generic GATT Battery Provider

## Purpose

Support devices that expose the Bluetooth SIG Battery Service.

This provider is not the primary AirPods path. It is a fallback and a general-device support layer.

## Target devices

- Generic Bluetooth headphones exposing Battery Service
- Mouse
- Keyboard
- Stylus
- Game controller
- Other BLE devices the user chooses to show

## Implementation

```text
src/BudsMonitor.Providers.Gatt/
  StandardGattBatteryProvider.cs
  WindowsGattClient.cs
  GattReadFailureMapper.cs
```

## Provider behavior

```csharp
public sealed class StandardGattBatteryProvider : IBatteryProvider
{
    public string ProviderId => "standard-gatt-battery";

    public async Task<ProviderProbeResult> ProbeAsync(
        DeviceCandidate device,
        CancellationToken cancellationToken)
    {
        // Try to discover Battery Service UUID 0x180F.
    }

    public async Task<BatteryReadResult> ReadAsync(
        DeviceCandidate device,
        CancellationToken cancellationToken)
    {
        // Read Battery Level characteristic UUID 0x2A19.
    }
}
```

## Read policy

- Use uncached reads when user explicitly refreshes.
- Use cached or normal reads for background refresh if uncached is too slow.
- Per-device timeout: 10 seconds.
- Same-device concurrency: 1.
- Retry: one retry after 3 seconds only for transient failure.

## Failure mapping

| Condition | Failure reason |
|---|---|
| Bluetooth radio off | `BluetoothOff` |
| Device disconnected | `DeviceNotConnected` |
| Service not found | `BatteryServiceNotFound` |
| Characteristic not found | `BatteryCharacteristicNotFound` |
| WinRT access denied | `GattAccessDenied` |
| Timeout | `GattTimeout` |
| Value length 0 | `InvalidBatteryPayload` |
| Value outside 0–100 | `InvalidBatteryPayload` |

## Snapshot mapping

```csharp
var snapshot = new BatterySnapshot
{
    StableDeviceKey = device.StableDeviceKey,
    DisplayName = device.DisplayName,
    DeviceKind = device.Kind,
    Source = BatteryDataSource.StandardGattBatteryService,
    Confidence = BatteryConfidence.Medium,
    MeasuredAt = clock.Now,
    ExpectedFreshness = TimeSpan.FromMinutes(5),
    Components = new[]
    {
        new BatteryComponent
        {
            Type = BatteryComponentType.WholeDevice,
            Percentage = batteryLevel,
            IsCharging = null,
            Label = "Battery"
        }
    }
};
```

## Device enumeration

Use a separate device watcher/registry layer. Do not make the GATT provider responsible for discovering every device.

The provider only answers:

```text
Can I read battery for this candidate?
What result did I read?
```

## Tests

- Battery value 0 is valid.
- Battery value 100 is valid.
- Battery value 101 is invalid.
- Empty payload fails.
- Service absence returns `NotApplicable`, not crash.
- Timeout returns `GattTimeout`.
- Read failures do not poison future reads.
