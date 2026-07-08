using BudsMonitor.Domain;

namespace BudsMonitor.Application;

/// <summary>
/// Decides when a battery snapshot should raise a low-battery notification.
/// Pure and deterministic apart from the per-device suppression state it keeps.
/// </summary>
public sealed class NotificationRuleService
{
    private readonly Dictionary<string, DateTimeOffset> _lastNotified = new();

    public IReadOnlyList<NotificationRequest> Evaluate(
        BatterySnapshot snapshot,
        NotificationRuleOptions options,
        DateTimeOffset now)
    {
        if (!options.Enabled)
        {
            return [];
        }

        if (options.QuietHoursEnabled && IsWithinQuietHours(TimeOnly.FromDateTime(now.LocalDateTime), options))
        {
            return [];
        }

        var isStale = snapshot.Source == BatteryDataSource.LastKnownCache
                      || (now - snapshot.MeasuredAt) > options.StaleAfter;
        if (isStale && !options.NotifyFromStaleData)
        {
            return [];
        }

        var low = snapshot.Components
            .Where(c => c.Percentage < ThresholdFor(c.Type, options))
            .ToList();
        if (low.Count == 0)
        {
            return [];
        }

        // Suppress repeat notifications for the same device within the window.
        if (_lastNotified.TryGetValue(snapshot.StableDeviceKey, out var last)
            && now - last < options.SuppressWindow)
        {
            return [];
        }

        _lastNotified[snapshot.StableDeviceKey] = now;

        var body = string.Join(", ", low.Select(c => $"{LabelFor(c.Type)} {c.Percentage}%"));
        return
        [
            new NotificationRequest
            {
                DeviceKey = snapshot.StableDeviceKey,
                Title = $"{snapshot.DisplayName} 배터리 부족",
                Body = body,
            },
        ];
    }

    private static int ThresholdFor(BatteryComponentType type, NotificationRuleOptions options)
        => type == BatteryComponentType.Case ? options.CaseThresholdPercent : options.EarbudThresholdPercent;

    private static string LabelFor(BatteryComponentType type) => type switch
    {
        BatteryComponentType.LeftBud => "왼쪽",
        BatteryComponentType.RightBud => "오른쪽",
        BatteryComponentType.Case => "케이스",
        _ => "배터리",
    };

    private static bool IsWithinQuietHours(TimeOnly now, NotificationRuleOptions options)
    {
        var start = options.QuietHoursStart;
        var end = options.QuietHoursEnd;

        // Non-wrapping window (e.g. 09:00–17:00) vs wrapping over midnight (22:00–07:00).
        return start <= end
            ? now >= start && now < end
            : now >= start || now < end;
    }
}
