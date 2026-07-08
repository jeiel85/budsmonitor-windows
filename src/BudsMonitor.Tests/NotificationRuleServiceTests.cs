using BudsMonitor.Application;
using BudsMonitor.Domain;

namespace BudsMonitor.Tests;

public sealed class NotificationRuleServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 8, 12, 0, 10, TimeSpan.Zero);

    private static readonly NotificationRuleOptions Options = new()
    {
        Enabled = true,
        EarbudThresholdPercent = 20,
        CaseThresholdPercent = 20,
        SuppressWindow = TimeSpan.FromMinutes(60),
        NotifyFromStaleData = false,
        StaleAfter = TimeSpan.FromSeconds(120),
    };

    private static BatterySnapshot Snapshot(
        int leftPercent,
        BatteryDataSource source = BatteryDataSource.AirPodsBleAdvertisement,
        DateTimeOffset? measuredAt = null)
        => new()
        {
            StableDeviceKey = "sha256:test",
            DisplayName = "AirPods Pro 2",
            DeviceKind = DeviceKind.Earbuds,
            Components = [new BatteryComponent { Type = BatteryComponentType.LeftBud, Percentage = leftPercent }],
            Source = source,
            Confidence = BatteryConfidence.High,
            MeasuredAt = measuredAt ?? Now,
        };

    [Fact]
    public void LowBattery_Notifies()
    {
        var service = new NotificationRuleService();

        var requests = service.Evaluate(Snapshot(10), Options, Now);

        var request = Assert.Single(requests);
        Assert.Contains("배터리 부족", request.Title);
        Assert.Contains("왼쪽 10%", request.Body);
    }

    [Fact]
    public void AboveThreshold_DoesNotNotify()
    {
        var service = new NotificationRuleService();
        Assert.Empty(service.Evaluate(Snapshot(50), Options, Now));
    }

    [Fact]
    public void RepeatWithinWindow_IsSuppressed()
    {
        var service = new NotificationRuleService();

        Assert.Single(service.Evaluate(Snapshot(10), Options, Now));
        // 30 min later, still fresh reading, but within the 60-min suppression window.
        Assert.Empty(service.Evaluate(Snapshot(9, measuredAt: Now.AddMinutes(30)), Options, Now.AddMinutes(30)));
    }

    [Fact]
    public void RepeatAfterWindow_NotifiesAgain()
    {
        var service = new NotificationRuleService();

        Assert.Single(service.Evaluate(Snapshot(10), Options, Now));
        Assert.Single(service.Evaluate(Snapshot(9, measuredAt: Now.AddMinutes(61)), Options, Now.AddMinutes(61)));
    }

    [Fact]
    public void StaleCache_DoesNotNotifyByDefault()
    {
        var service = new NotificationRuleService();
        Assert.Empty(service.Evaluate(Snapshot(10, BatteryDataSource.LastKnownCache), Options, Now));
    }

    [Fact]
    public void StaleCache_NotifiesWhenEnabled()
    {
        var service = new NotificationRuleService();
        var options = Options with { NotifyFromStaleData = true };
        Assert.Single(service.Evaluate(Snapshot(10, BatteryDataSource.LastKnownCache), options, Now));
    }

    [Fact]
    public void Disabled_DoesNotNotify()
    {
        var service = new NotificationRuleService();
        var options = Options with { Enabled = false };
        Assert.Empty(service.Evaluate(Snapshot(10), options, Now));
    }

    [Fact]
    public void OldMeasurement_TreatedAsStale()
    {
        var service = new NotificationRuleService();
        // Measured 10 minutes ago -> age exceeds the 120s stale threshold.
        Assert.Empty(service.Evaluate(Snapshot(10, measuredAt: Now.AddMinutes(-10)), Options, Now));
    }
}
