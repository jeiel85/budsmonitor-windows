using BudsMonitor.Infrastructure.Devices;

namespace BudsMonitor.App;

/// <summary>
/// In-memory device registry backed by devices.json. Tracks first/last seen and the
/// user's alias/pin/hidden preferences. New devices and preference changes are saved
/// immediately; last-seen updates are flushed on exit.
/// </summary>
internal sealed class DeviceRegistry
{
    private readonly DeviceRegistryRepository _repository;
    private readonly Dictionary<string, DeviceRegistryEntry> _entries = new();

    public DeviceRegistry(DeviceRegistryRepository repository)
    {
        _repository = repository;
        foreach (var entry in _repository.Load().Devices)
        {
            _entries[entry.StableDeviceKey] = entry;
        }
    }

    public DeviceRegistryEntry RecordSeen(string key, string displayName, string? providerHint, DateTimeOffset now)
    {
        if (_entries.TryGetValue(key, out var existing))
        {
            var updated = existing with { LastSeenAt = now, DisplayName = displayName };
            _entries[key] = updated;
            return updated;
        }

        var entry = new DeviceRegistryEntry
        {
            StableDeviceKey = key,
            DisplayName = displayName,
            ProviderHint = providerHint,
            FirstSeenAt = now,
            LastSeenAt = now,
        };
        _entries[key] = entry;
        Save();
        return entry;
    }

    public DeviceRegistryEntry? Get(string key) => _entries.GetValueOrDefault(key);

    public void SetPinned(string key, bool pinned) => Mutate(key, e => e with { IsPinned = pinned });

    public void SetHidden(string key, bool hidden) => Mutate(key, e => e with { IsHidden = hidden });

    public void SetAlias(string key, string? alias)
        => Mutate(key, e => e with { Alias = string.IsNullOrWhiteSpace(alias) ? null : alias.Trim() });

    public void Save() => _repository.Save(new DeviceRegistryFile { Devices = _entries.Values.ToList() });

    private void Mutate(string key, Func<DeviceRegistryEntry, DeviceRegistryEntry> mutate)
    {
        if (_entries.TryGetValue(key, out var entry))
        {
            _entries[key] = mutate(entry);
            Save();
        }
    }
}
