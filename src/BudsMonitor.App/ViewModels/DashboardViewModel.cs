using System.Collections.ObjectModel;
using System.ComponentModel;
using BudsMonitor.Domain;

namespace BudsMonitor.App.ViewModels;

/// <summary>
/// Top-level dashboard state: device cards plus an empty-state flag. Applies the user's
/// alias/pin/hidden preferences and keeps pinned devices first.
/// </summary>
public sealed class DashboardViewModel : INotifyPropertyChanged
{
    public ObservableCollection<DeviceCardViewModel> Devices { get; } = [];

    public bool HasDevices => Devices.Count > 0;

    public void Upsert(
        BatterySnapshot snapshot,
        string? alias,
        bool isPinned,
        bool isHidden,
        bool showHidden,
        DateTimeOffset now)
    {
        var key = snapshot.StableDeviceKey;
        var existing = Devices.FirstOrDefault(d => d.StableDeviceKey == key);

        if (isHidden && !showHidden)
        {
            if (existing is not null)
            {
                Devices.Remove(existing);
                Notify(nameof(HasDevices));
            }
            return;
        }

        if (existing is null)
        {
            var card = new DeviceCardViewModel(key);
            card.Update(snapshot, alias, isPinned, now);
            Devices.Add(card);
            Notify(nameof(HasDevices));
        }
        else
        {
            existing.Update(snapshot, alias, isPinned, now);
        }

        SortPinnedFirst();
    }

    public void RefreshFreshness(DateTimeOffset now)
    {
        foreach (var device in Devices)
        {
            device.RefreshFreshness(now);
        }
    }

    private void SortPinnedFirst()
    {
        var sorted = Devices
            .OrderByDescending(d => d.IsPinned)
            .ThenBy(d => d.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();

        for (var target = 0; target < sorted.Count; target++)
        {
            var current = Devices.IndexOf(sorted[target]);
            if (current != target)
            {
                Devices.Move(current, target);
            }
        }
    }

    /// <summary>Short one-line summary for the tray tooltip (first device).</summary>
    public string BuildTraySummary()
    {
        if (Devices.Count == 0)
        {
            return "BudsMonitor · 감지된 기기 없음";
        }

        var device = Devices[0];
        var parts = new List<string>();
        if (device.HasLeft) parts.Add($"L {device.LeftValue}");
        if (device.HasRight) parts.Add($"R {device.RightValue}");
        if (device.HasCase) parts.Add($"C {device.CaseValue}");
        if (device.HasWholeDevice) parts.Add(device.WholeDeviceValue);

        var battery = parts.Count > 0 ? string.Join(" ", parts) : "배터리 정보 없음";
        return $"{device.DisplayName} · {battery} · {device.FreshnessBadge}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
