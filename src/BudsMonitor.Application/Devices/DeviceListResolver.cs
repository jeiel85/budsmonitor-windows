using BudsMonitor.Domain;

namespace BudsMonitor.Application.Devices;

/// <summary>
/// Collapses the raw device stream into the dashboard's card set. Earbud advertisements
/// (AirPods / Galaxy Buds) rotate their address and arrive from every nearby pair, so they are
/// aggregated to ONE card per family showing the strongest-signal (nearest = yours) reading, and
/// optionally filtered to the families you have paired to this PC. Connection-based devices
/// (standard GATT) pass through with their own stable key.
/// </summary>
public sealed class DeviceListResolver
{
    private static readonly TimeSpan FreshWindow = TimeSpan.FromSeconds(30);
    private readonly Dictionary<string, Best> _familyBest = new();

    /// <summary>Earbud families paired to this PC; only these show when <see cref="ShowPairedOnly"/> is true.</summary>
    public IReadOnlySet<string> PairedFamilies { get; set; } = new HashSet<string>();

    /// <summary>When true, earbud advertisements are limited to <see cref="PairedFamilies"/>.</summary>
    public bool ShowPairedOnly { get; set; } = true;

    /// <summary>Forgets the per-family "nearest" state (e.g. after a filter change).</summary>
    public void Reset() => _familyBest.Clear();

    /// <summary>
    /// Returns the dashboard card key for <paramref name="snapshot"/>, or null to drop it
    /// (filtered out, or a nearer/fresher same-family reading currently holds the card).
    /// </summary>
    public string? Resolve(BatterySnapshot snapshot, DateTimeOffset now)
    {
        var family = EarbudFamily.Of(snapshot.DisplayName) ?? EarbudFamily.Of(snapshot.ModelName);
        var isEarbudAdvertisement = family is not null && snapshot.Source is
            BatteryDataSource.AirPodsBleAdvertisement
            or BatteryDataSource.GalaxyBudsProvider
            or BatteryDataSource.LastKnownCache;

        if (!isEarbudAdvertisement)
        {
            return snapshot.StableDeviceKey; // connection-based device keeps its stable key
        }

        // Only filter when we actually know the paired families; if enumeration found none,
        // fall back to showing all (deduped) so the dashboard is never mysteriously empty.
        if (ShowPairedOnly && PairedFamilies.Count > 0 && !PairedFamilies.Contains(family!))
        {
            return null;
        }

        var effective = snapshot.Rssi ?? int.MinValue;
        if (_familyBest.TryGetValue(family!, out var best)
            && (now - best.AppliedAt) <= FreshWindow
            && (effective < best.Rssi
                || (effective == best.Rssi && snapshot.MeasuredAt < best.MeasuredAt)))
        {
            return null; // a nearer/fresher same-family reading holds the card
        }

        _familyBest[family!] = new Best(effective, snapshot.MeasuredAt, now);
        return "family:" + family;
    }

    private readonly record struct Best(int Rssi, DateTimeOffset MeasuredAt, DateTimeOffset AppliedAt);
}
