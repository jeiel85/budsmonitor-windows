namespace BudsMonitor.Infrastructure.Devices;

/// <summary>
/// Persisted device registry (see docs/09-storage-privacy device registry schema).
/// Tracks user preferences (alias/pin/hidden) and first/last seen per device.
/// Stored at %AppData%\BudsMonitor\devices.json.
/// </summary>
public sealed record DeviceRegistryFile
{
    public int Version { get; init; } = 1;
    public IReadOnlyList<DeviceRegistryEntry> Devices { get; init; } = [];
}

public sealed record DeviceRegistryEntry
{
    public required string StableDeviceKey { get; init; }
    public required string DisplayName { get; init; }
    public string? Alias { get; init; }
    public string? ProviderHint { get; init; }
    public bool IsPinned { get; init; }
    public bool IsHidden { get; init; }
    public DateTimeOffset FirstSeenAt { get; init; }
    public DateTimeOffset LastSeenAt { get; init; }
}
