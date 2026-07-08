# 05. AirPods BLE Advertisement Provider

## Purpose

Provide first-class AirPods support using BLE proximity advertisements, based on the previous `librepods-windows-ble` work.

## Scope

### Supported

- AirPods model identification where model id is present
- Left pod battery where payload provides it
- Right pod battery where payload provides it
- Case battery where payload provides it
- Charging state where payload provides it
- In-ear state where payload provides it
- Connection/state hint where payload provides it
- Last-seen and stale handling

### Not supported

- ANC control
- Transparency control
- Conversation awareness control
- AirPods rename
- Firmware metadata
- Pairing management

## Implementation source

Start from the existing C# feasibility parser in:

```text
experiments/windows-feasibility/battery-tray-mvp/Program.cs
```

Then move it into:

```text
src/BudsMonitor.Providers.AirPods/
  AirPodsAdvertisementParser.cs
  AirPodsAdvertisementSnapshot.cs
  AirPodsModelCatalog.cs
  AirPodsBatteryMapper.cs
  AirPodsBleAdvertisementProvider.cs
```

## Scanner design

```csharp
public sealed class BleAdvertisementScannerService : IHostedAppService, IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher _watcher;
    private readonly ChannelWriter<BleAdvertisementFrame> _writer;

    public void Start()
    {
        _watcher.ScanningMode = BluetoothLEScanningMode.Active;
        _watcher.Received += OnReceived;
        _watcher.Stopped += OnStopped;
        _watcher.Start();
    }

    private void OnReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        foreach (var section in args.Advertisement.ManufacturerData)
        {
            var frame = BleAdvertisementFrame.FromWinRt(args, section);
            _writer.TryWrite(frame);
        }
    }
}
```

## Apple manufacturer filter

```csharp
private const ushort AppleCompanyId = 0x004C;
```

Only pass Apple manufacturer sections to the AirPods parser.

## Parser contract

```csharp
public static class AirPodsAdvertisementParser
{
    public static bool TryParse(
        ReadOnlySpan<byte> data,
        AirPodsAdvertisementContext context,
        out AirPodsAdvertisementSnapshot snapshot);
}
```

```csharp
public sealed record AirPodsAdvertisementContext
{
    public required DateTimeOffset ReceivedAt { get; init; }
    public required ulong BluetoothAddress { get; init; }
    public string? LocalName { get; init; }
    public short? RawRssi { get; init; }
}
```

```csharp
public sealed record AirPodsAdvertisementSnapshot
{
    public required ushort ModelId { get; init; }
    public required string ModelName { get; init; }
    public int? LeftBattery { get; init; }
    public int? RightBattery { get; init; }
    public int? CaseBattery { get; init; }
    public bool? LeftCharging { get; init; }
    public bool? RightCharging { get; init; }
    public bool? CaseCharging { get; init; }
    public bool? LeftInEar { get; init; }
    public bool? RightInEar { get; init; }
    public string? LidState { get; init; }
    public string? ConnectionState { get; init; }
    public byte[] RawPayload { get; init; } = [];
    public DateTimeOffset ReceivedAt { get; init; }
}
```

## Battery precision

AirPods BLE proximity battery values may be 10% increments depending on payload interpretation.

UI rule:

- Display `80%` normally.
- In diagnostics, record `precision: 10` where applicable.
- Avoid implying 1% precision if source is advertisement nibble.

## Mapping to generic snapshot

```csharp
public BatterySnapshot ToBatterySnapshot(AirPodsAdvertisementSnapshot airpods)
{
    var components = new List<BatteryComponent>();

    if (airpods.LeftBattery is int left)
    {
        components.Add(new BatteryComponent
        {
            Type = BatteryComponentType.LeftBud,
            Percentage = left,
            IsCharging = airpods.LeftCharging,
            Label = "Left"
        });
    }

    if (airpods.RightBattery is int right)
    {
        components.Add(new BatteryComponent
        {
            Type = BatteryComponentType.RightBud,
            Percentage = right,
            IsCharging = airpods.RightCharging,
            Label = "Right"
        });
    }

    if (airpods.CaseBattery is int c)
    {
        components.Add(new BatteryComponent
        {
            Type = BatteryComponentType.Case,
            Percentage = c,
            IsCharging = airpods.CaseCharging,
            Label = "Case"
        });
    }

    return new BatterySnapshot
    {
        StableDeviceKey = BuildStableKey(airpods),
        DisplayName = BuildDisplayName(airpods),
        DeviceKind = DeviceKind.Earbuds,
        Components = components,
        Source = BatteryDataSource.AirPodsBleAdvertisement,
        Confidence = BatteryConfidence.High,
        MeasuredAt = airpods.ReceivedAt,
        ExpectedFreshness = TimeSpan.FromSeconds(30),
        ModelName = airpods.ModelName
    };
}
```

## Stale handling

AirPods provider is event-driven. It should not show disconnected immediately when no packet appears.

Recommended thresholds:

| Last packet age | UI state |
|---:|---|
| 0–30 seconds | Live |
| 30–120 seconds | Recently seen |
| 2–15 minutes | Stale |
| 15+ minutes | Last known only |

## Multi-device behavior

Support multiple AirPods-like devices by keying snapshots with a stable fingerprint.

Candidate key components:

```text
provider=airpods-ble
modelId
localName normalized
bluetoothAddress hash, if stable
recent packet signature hash
```

If multiple devices cannot be distinguished confidently, show a conflict state and let user assign alias/pin manually.

## Tests

Required tests:

- Parser rejects non-AirPods Apple payloads.
- Parser rejects too-short payloads.
- Parser maps known model ids.
- Parser maps battery nibble `15` to unavailable.
- Parser maps battery nibble `0..10` to `0..100`.
- Parser handles left/right primary flip.
- Parser handles missing local name.
- Provider emits stale state after timeout.

## Diagnostics

For AirPods diagnostics, export:

```json
{
  "provider": "airpods-ble-advertisement",
  "modelId": "0x1420",
  "modelName": "AirPods Pro 2 Lightning",
  "components": ["left", "right", "case"],
  "batteryPrecisionPercent": 10,
  "rawPayloadHexMasked": "...",
  "receivedAt": "...",
  "rssi": -58
}
```

Mask or hash device address by default.
