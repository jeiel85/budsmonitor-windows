# 04. Provider Model

## Provider priority

```text
1. AirPodsBleAdvertisementProvider
2. GalaxyBudsProvider
3. StandardGattBatteryProvider
4. LastKnownBatteryProvider
```

## Why provider priority matters

AirPods should not be treated as generic GATT devices first. The existing repository has already shown AirPods proximity advertisements can expose useful data. Therefore AirPods BLE advertisement parsing is the primary path for AirPods.

Generic GATT is a fallback for:

- BLE headphones exposing Battery Service
- mouse/keyboard/pen devices if the user chooses to show them
- unknown devices with standard battery data

## Core contracts

```csharp
public interface IBatteryProvider
{
    string ProviderId { get; }
    ProviderKind Kind { get; }

    Task<ProviderProbeResult> ProbeAsync(
        DeviceCandidate device,
        CancellationToken cancellationToken);

    Task<BatteryReadResult> ReadAsync(
        DeviceCandidate device,
        CancellationToken cancellationToken);
}
```

```csharp
public interface IAdvertisementBatteryProvider
{
    string ProviderId { get; }

    bool TryParseAdvertisement(
        BleAdvertisementFrame frame,
        out BatteryReadResult result);
}
```

```csharp
public sealed record ProviderProbeResult(
    string ProviderId,
    bool CanHandle,
    BatteryReadFailureReason? FailureReason,
    string? Message);
```

```csharp
public sealed record BatteryReadResult
{
    public required string ProviderId { get; init; }
    public required BatteryReadStatus Status { get; init; }
    public BatterySnapshot? Snapshot { get; init; }
    public BatteryReadFailure? Failure { get; init; }
}
```

```csharp
public enum BatteryReadStatus
{
    Success,
    NotApplicable,
    TemporarilyUnavailable,
    Failed
}
```

```csharp
public enum BatteryReadFailureReason
{
    None,
    DeviceNotConnected,
    BluetoothOff,
    DeviceAsleep,
    AdvertisementNotRecognized,
    BatteryServiceNotFound,
    BatteryCharacteristicNotFound,
    GattAccessDenied,
    GattTimeout,
    InvalidBatteryPayload,
    ProviderProtocolMismatch,
    UnsupportedDevice,
    UnknownError
}
```

## Battery snapshot model

```csharp
public sealed record BatterySnapshot
{
    public required string StableDeviceKey { get; init; }
    public required string DisplayName { get; init; }
    public required DeviceKind DeviceKind { get; init; }
    public required IReadOnlyList<BatteryComponent> Components { get; init; }
    public required BatteryDataSource Source { get; init; }
    public required BatteryConfidence Confidence { get; init; }
    public required DateTimeOffset MeasuredAt { get; init; }
    public TimeSpan? ExpectedFreshness { get; init; }
    public string? ModelName { get; init; }
    public string? ProviderDiagnosticCode { get; init; }
}
```

```csharp
public sealed record BatteryComponent
{
    public required BatteryComponentType Type { get; init; }
    public required int Percentage { get; init; }
    public bool? IsCharging { get; init; }
    public string? Label { get; init; }
}
```

```csharp
public enum BatteryComponentType
{
    WholeDevice,
    LeftBud,
    RightBud,
    Case,
    Unknown
}
```

```csharp
public enum BatteryDataSource
{
    AirPodsBleAdvertisement,
    GalaxyBudsProvider,
    StandardGattBatteryService,
    LastKnownCache
}
```

```csharp
public enum BatteryConfidence
{
    Low,
    Medium,
    High
}
```

## Device key strategy

Do not use raw Bluetooth address as the public user-facing key.

Internal model:

```csharp
public sealed record DeviceIdentity
{
    public required string StableDeviceKey { get; init; } // hashed when persisted/logged
    public string? BluetoothAddress { get; init; }       // in memory only where possible
    public string? WindowsDeviceId { get; init; }
    public required string DisplayName { get; init; }
    public string? Alias { get; init; }
}
```

For AirPods advertisements, the Bluetooth address may rotate or not be stable depending on OS/device privacy behavior. The resolver should combine:

- model id
- display/local name if present
- recent address hash
- provider fingerprint
- user alias/pinning

## Last-known fallback behavior

`LastKnownBatteryProvider` is a display fallback, not a real hardware provider.

Rules:

- Use it only when a real provider fails or is silent.
- Mark snapshot source as `LastKnownCache`.
- UI must show stale state.
- Notifications must not fire from stale cache unless explicitly configured.

## Provider acceptance gates

A provider is production-ready only when it has:

- Unit tests for parser edge cases
- Real-device diagnostic samples
- Failure reason mapping
- Cache/stale handling
- No unbounded background loops
- No network dependency
