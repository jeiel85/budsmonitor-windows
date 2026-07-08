using System.Collections.ObjectModel;
using System.ComponentModel;
using BudsMonitor.Domain;

namespace BudsMonitor.App.ViewModels;

/// <summary>
/// Top-level dashboard state: the set of device cards plus an empty-state flag.
/// The app calls <see cref="Upsert"/> from the frame consumer and
/// <see cref="RefreshFreshness"/> from a timer.
/// </summary>
public sealed class DashboardViewModel : INotifyPropertyChanged
{
    public ObservableCollection<DeviceCardViewModel> Devices { get; } = [];

    public bool HasDevices => Devices.Count > 0;

    public void Upsert(BatterySnapshot snapshot, DateTimeOffset now)
    {
        var existing = Devices.FirstOrDefault(d => d.StableDeviceKey == snapshot.StableDeviceKey);
        if (existing is null)
        {
            var card = new DeviceCardViewModel(snapshot.StableDeviceKey);
            card.Update(snapshot, now);
            Devices.Add(card);
            Notify(nameof(HasDevices));
        }
        else
        {
            existing.Update(snapshot, now);
        }
    }

    public void RefreshFreshness(DateTimeOffset now)
    {
        foreach (var device in Devices)
        {
            device.RefreshFreshness(now);
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

        var battery = parts.Count > 0 ? string.Join(" ", parts) : "배터리 정보 없음";
        return $"{device.DisplayName} · {battery} · {device.FreshnessBadge}";
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify(string name)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
