using BudsMonitor.Domain;
using BudsMonitor.Infrastructure.Cache;

namespace BudsMonitor.App;

/// <summary>Maps between the domain BatterySnapshot and the persisted cache DTO.</summary>
internal static class CacheMapping
{
    public static CachedBatterySnapshot ToCache(BatterySnapshot snapshot) => new()
    {
        StableDeviceKey = snapshot.StableDeviceKey,
        DisplayName = snapshot.DisplayName,
        Source = snapshot.Source.ToString(),
        MeasuredAt = snapshot.MeasuredAt,
        ExpectedFreshnessSeconds = snapshot.ExpectedFreshness is { } freshness
            ? (int)freshness.TotalSeconds
            : null,
        Components = snapshot.Components.Select(c => new CachedBatteryComponent
        {
            Type = c.Type.ToString(),
            Percentage = c.Percentage,
            IsCharging = c.IsCharging,
        }).ToList(),
    };

    /// <summary>Rehydrates a cached snapshot as a last-known reading (marked stale on display).</summary>
    public static BatterySnapshot ToDomain(CachedBatterySnapshot cached) => new()
    {
        StableDeviceKey = cached.StableDeviceKey,
        DisplayName = cached.DisplayName,
        DeviceKind = DeviceKind.Earbuds,
        Components = cached.Components.Select(c => new BatteryComponent
        {
            Type = Enum.TryParse<BatteryComponentType>(c.Type, out var type) ? type : BatteryComponentType.Unknown,
            Percentage = c.Percentage,
            IsCharging = c.IsCharging,
        }).ToList(),
        Source = BatteryDataSource.LastKnownCache,
        Confidence = BatteryConfidence.Low,
        MeasuredAt = cached.MeasuredAt,
        ExpectedFreshness = cached.ExpectedFreshnessSeconds is { } seconds
            ? TimeSpan.FromSeconds(seconds)
            : null,
    };
}
