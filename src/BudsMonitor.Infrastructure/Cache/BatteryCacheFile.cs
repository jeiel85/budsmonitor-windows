namespace BudsMonitor.Infrastructure.Cache;

/// <summary>
/// Persisted last-known battery snapshots (see docs/09-storage-privacy battery cache
/// schema). This is a storage-only DTO; mapping to the domain BatterySnapshot happens
/// in the provider integration goal. Stored at
/// %LocalAppData%\BudsMonitor\cache\battery-cache.json.
/// </summary>
public sealed record BatteryCacheFile
{
    public int Version { get; init; } = 1;
    public IReadOnlyList<CachedBatterySnapshot> Snapshots { get; init; } = [];
}

public sealed record CachedBatterySnapshot
{
    public required string StableDeviceKey { get; init; }
    public required string DisplayName { get; init; }
    public required string Source { get; init; }
    public required DateTimeOffset MeasuredAt { get; init; }
    public int? ExpectedFreshnessSeconds { get; init; }
    public IReadOnlyList<CachedBatteryComponent> Components { get; init; } = [];
}

public sealed record CachedBatteryComponent
{
    public required string Type { get; init; }
    public required int Percentage { get; init; }
    public bool? IsCharging { get; init; }
}
