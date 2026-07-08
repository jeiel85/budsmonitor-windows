namespace BudsMonitor.Domain;

/// <summary>
/// A single manufacturer-data section from a received BLE advertisement, normalized off
/// the WinRT watcher event so higher layers do not depend on WinRT types.
/// </summary>
public sealed record BleAdvertisementFrame
{
    public required DateTimeOffset ReceivedAt { get; init; }
    public required ushort CompanyId { get; init; }
    public required byte[] ManufacturerData { get; init; }
    public required ulong BluetoothAddress { get; init; }
    public string? LocalName { get; init; }
    public short? RawRssi { get; init; }
}
