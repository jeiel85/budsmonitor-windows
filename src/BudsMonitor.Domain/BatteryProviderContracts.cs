namespace BudsMonitor.Domain;

/// <summary>
/// A provider that derives battery state from a BLE advertisement frame (no connection).
/// Implemented by the AirPods proximity provider; GATT/connection providers use a
/// separate contract added in a later goal.
/// </summary>
public interface IAdvertisementBatteryProvider
{
    string ProviderId { get; }

    bool TryParseAdvertisement(BleAdvertisementFrame frame, out BatteryReadResult result);
}

/// <summary>
/// A provider that reads battery over a connection (GATT). <see cref="ProbeAsync"/>
/// checks whether the device is supported; <see cref="ReadAsync"/> performs the read.
/// </summary>
public interface IBatteryProvider
{
    string ProviderId { get; }
    ProviderKind Kind { get; }

    Task<ProviderProbeResult> ProbeAsync(DeviceCandidate device, CancellationToken cancellationToken);

    Task<BatteryReadResult> ReadAsync(DeviceCandidate device, CancellationToken cancellationToken);
}

public enum ProviderKind
{
    Advertisement,
    Gatt,
    Cache,
}

public sealed record ProviderProbeResult(
    string ProviderId,
    bool CanHandle,
    BatteryReadFailureReason? FailureReason,
    string? Message);

/// <summary>A device the resolver may ask a provider to read battery for.</summary>
public sealed record DeviceCandidate
{
    public required string StableDeviceKey { get; init; }
    public required string DisplayName { get; init; }
    public DeviceKind Kind { get; init; }
    public string? WindowsDeviceId { get; init; }
    public string? BluetoothAddress { get; init; }
}

public sealed record BatteryReadResult
{
    public required string ProviderId { get; init; }
    public required BatteryReadStatus Status { get; init; }
    public BatterySnapshot? Snapshot { get; init; }
    public BatteryReadFailure? Failure { get; init; }

    public static BatteryReadResult NotApplicable(string providerId) => new()
    {
        ProviderId = providerId,
        Status = BatteryReadStatus.NotApplicable,
    };

    public static BatteryReadResult Success(string providerId, BatterySnapshot snapshot) => new()
    {
        ProviderId = providerId,
        Status = BatteryReadStatus.Success,
        Snapshot = snapshot,
    };

    public static BatteryReadResult Failed(string providerId, BatteryReadFailureReason reason, string message) => new()
    {
        ProviderId = providerId,
        Status = BatteryReadStatus.Failed,
        Failure = new BatteryReadFailure(reason, message),
    };
}

public enum BatteryReadStatus
{
    Success,
    NotApplicable,
    TemporarilyUnavailable,
    Failed,
}

public sealed record BatteryReadFailure(
    BatteryReadFailureReason Reason,
    string Message,
    Exception? Exception = null);

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
    UnknownError,
}
