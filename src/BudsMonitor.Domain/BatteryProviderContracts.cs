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
