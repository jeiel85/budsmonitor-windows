namespace BudsMonitor.Providers.AirPods;

/// <summary>
/// Parsed AirPods BLE proximity advertisement. Battery values have 10% precision and
/// are null when the payload reports them unavailable. This is a provider-local model;
/// mapping to the domain BatterySnapshot happens in the provider integration goal.
/// </summary>
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
    public bool PrimaryLeft { get; init; }
    public string? LidState { get; init; }
    public string? ConnectionState { get; init; }
    public byte[] RawPayload { get; init; } = [];
    public DateTimeOffset ReceivedAt { get; init; }
}
