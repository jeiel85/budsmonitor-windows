namespace BudsMonitor.Domain;

/// <summary>
/// A normalized battery reading for a device from a single provider. Component values
/// are per-part (left/right/case or whole device); a missing part is simply absent
/// from <see cref="Components"/> (never reported as 0%).
/// </summary>
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

    /// <summary>Advertisement signal strength (dBm) when available; used to prefer the nearest
    /// device among same-model earbuds. Null for connection/cache-based readings.</summary>
    public int? Rssi { get; init; }
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
    Unknown,
}

public enum BatteryDataSource
{
    AirPodsBleAdvertisement,
    GalaxyBudsProvider,
    StandardGattBatteryService,
    LastKnownCache,
}

public enum BatteryConfidence
{
    Low,
    Medium,
    High,
}

public enum DeviceKind
{
    Unknown,
    Earbuds,
    Headphones,
    Mouse,
    Keyboard,
    Pen,
    GameController,
}
