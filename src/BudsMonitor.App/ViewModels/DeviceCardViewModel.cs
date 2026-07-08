using System.ComponentModel;
using System.Runtime.CompilerServices;
using BudsMonitor.Domain;

namespace BudsMonitor.App.ViewModels;

/// <summary>
/// Bindable state for a single device card. Values are refreshed from a
/// <see cref="BatterySnapshot"/>; freshness/dimming are recomputed on a timer so
/// stale data is marked without new packets.
/// </summary>
public sealed class DeviceCardViewModel : INotifyPropertyChanged
{
    public DeviceCardViewModel(string stableDeviceKey) => StableDeviceKey = stableDeviceKey;

    public string StableDeviceKey { get; }

    public string DisplayName { get; private set; } = "";
    public string LeftValue { get; private set; } = "—";
    public string RightValue { get; private set; } = "—";
    public string CaseValue { get; private set; } = "—";
    public bool HasLeft { get; private set; }
    public bool HasRight { get; private set; }
    public bool HasCase { get; private set; }
    public bool LeftCharging { get; private set; }
    public bool RightCharging { get; private set; }
    public bool CaseCharging { get; private set; }
    public bool HasWholeDevice { get; private set; }
    public string WholeDeviceValue { get; private set; } = "—";
    public bool WholeDeviceCharging { get; private set; }
    public bool IsPinned { get; private set; }
    public string StatusLine { get; private set; } = "";
    public string FreshnessBadge { get; private set; } = "";
    public double CardOpacity { get; private set; } = 1.0;

    public DateTimeOffset MeasuredAt { get; private set; }
    public BatteryDataSource Source { get; private set; }

    public void Update(BatterySnapshot snapshot, string? alias, bool isPinned, DateTimeOffset now)
    {
        DisplayName = string.IsNullOrWhiteSpace(alias) ? snapshot.DisplayName : alias;
        IsPinned = isPinned;
        MeasuredAt = snapshot.MeasuredAt;
        Source = snapshot.Source;

        var left = Find(snapshot, BatteryComponentType.LeftBud);
        var right = Find(snapshot, BatteryComponentType.RightBud);
        var caseComp = Find(snapshot, BatteryComponentType.Case);

        HasLeft = left is not null;
        LeftValue = Format(left);
        LeftCharging = left?.IsCharging ?? false;

        HasRight = right is not null;
        RightValue = Format(right);
        RightCharging = right?.IsCharging ?? false;

        HasCase = caseComp is not null;
        CaseValue = Format(caseComp);
        CaseCharging = caseComp?.IsCharging ?? false;

        var whole = Find(snapshot, BatteryComponentType.WholeDevice);
        HasWholeDevice = whole is not null;
        WholeDeviceValue = Format(whole);
        WholeDeviceCharging = whole?.IsCharging ?? false;

        RefreshFreshness(now);
        RaiseAll();
    }

    /// <summary>Recomputes the freshness badge and dimming from the current age.</summary>
    public void RefreshFreshness(DateTimeOffset now)
    {
        var age = now - MeasuredAt;
        var (badge, opacity) = Classify(age, Source);
        FreshnessBadge = badge;
        CardOpacity = opacity;
        StatusLine = $"{DisplayName} · {DescribeAge(age)}";
        Notify(nameof(FreshnessBadge));
        Notify(nameof(CardOpacity));
        Notify(nameof(StatusLine));
    }

    private static BatteryComponent? Find(BatterySnapshot snapshot, BatteryComponentType type)
        => snapshot.Components.FirstOrDefault(c => c.Type == type);

    private static string Format(BatteryComponent? component)
        => component is null ? "—" : $"{component.Percentage}%";

    // Stale thresholds per docs/05-airpods-provider.
    private static (string badge, double opacity) Classify(TimeSpan age, BatteryDataSource source)
    {
        if (source == BatteryDataSource.LastKnownCache)
        {
            return ("마지막 값", 0.55);
        }

        return age switch
        {
            _ when age < TimeSpan.FromSeconds(30) => ("실시간", 1.0),
            _ when age < TimeSpan.FromSeconds(120) => ("방금", 1.0),
            _ when age < TimeSpan.FromMinutes(15) => ("오래됨", 0.55),
            _ => ("마지막 값", 0.55),
        };
    }

    private static string DescribeAge(TimeSpan age) => age.TotalSeconds switch
    {
        < 60 => "방금 전",
        < 3600 => $"{(int)age.TotalMinutes}분 전",
        _ => $"{(int)age.TotalHours}시간 전",
    };

    private void RaiseAll()
    {
        foreach (var name in new[]
        {
            nameof(DisplayName), nameof(IsPinned), nameof(LeftValue), nameof(RightValue), nameof(CaseValue),
            nameof(HasLeft), nameof(HasRight), nameof(HasCase),
            nameof(LeftCharging), nameof(RightCharging), nameof(CaseCharging),
            nameof(HasWholeDevice), nameof(WholeDeviceValue), nameof(WholeDeviceCharging),
            nameof(StatusLine), nameof(FreshnessBadge), nameof(CardOpacity),
        })
        {
            Notify(name);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void Notify([CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
