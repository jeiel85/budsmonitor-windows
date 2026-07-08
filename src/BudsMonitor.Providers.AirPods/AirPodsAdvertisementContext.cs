namespace BudsMonitor.Providers.AirPods;

/// <summary>
/// Context captured alongside a raw Apple manufacturer advertisement section, passed
/// to the parser so the resulting snapshot can carry timing/identity information.
/// </summary>
public sealed record AirPodsAdvertisementContext
{
    public required DateTimeOffset ReceivedAt { get; init; }
    public required ulong BluetoothAddress { get; init; }
    public string? LocalName { get; init; }
    public short? RawRssi { get; init; }
}
