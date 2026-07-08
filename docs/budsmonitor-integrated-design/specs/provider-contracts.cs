namespace BudsMonitor.Domain;

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

public interface IAdvertisementBatteryProvider
{
    string ProviderId { get; }

    bool TryParseAdvertisement(
        BleAdvertisementFrame frame,
        out BatteryReadResult result);
}

public enum ProviderKind
{
    Advertisement,
    Gatt,
    Cache
}

public sealed record ProviderProbeResult(
    string ProviderId,
    bool CanHandle,
    BatteryReadFailureReason? FailureReason,
    string? Message);

public sealed record BatteryReadResult
{
    public required string ProviderId { get; init; }
    public required BatteryReadStatus Status { get; init; }
    public BatterySnapshot? Snapshot { get; init; }
    public BatteryReadFailure? Failure { get; init; }
}

public enum BatteryReadStatus
{
    Success,
    NotApplicable,
    TemporarilyUnavailable,
    Failed
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
    UnknownError
}

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

public sealed record BatteryComponent
{
    public required BatteryComponentType Type { get; init; }
    public required int Percentage { get; init; }
    public bool? IsCharging { get; init; }
    public string? Label { get; init; }
}

public enum BatteryComponentType
{
    WholeDevice,
    LeftBud,
    RightBud,
    Case,
    Unknown
}

public enum BatteryDataSource
{
    AirPodsBleAdvertisement,
    GalaxyBudsProvider,
    StandardGattBatteryService,
    LastKnownCache
}

public enum BatteryConfidence
{
    Low,
    Medium,
    High
}

public enum DeviceKind
{
    Unknown,
    Earbuds,
    Headphones,
    Mouse,
    Keyboard,
    Pen,
    GameController
}

public sealed record DeviceCandidate
{
    public required string StableDeviceKey { get; init; }
    public required string DisplayName { get; init; }
    public DeviceKind Kind { get; init; }
    public string? WindowsDeviceId { get; init; }
    public string? BluetoothAddress { get; init; }
}

public sealed record BleAdvertisementFrame
{
    public required DateTimeOffset ReceivedAt { get; init; }
    public required ushort CompanyId { get; init; }
    public required byte[] ManufacturerData { get; init; }
    public required ulong BluetoothAddress { get; init; }
    public string? LocalName { get; init; }
    public short? RawRssi { get; init; }
}
