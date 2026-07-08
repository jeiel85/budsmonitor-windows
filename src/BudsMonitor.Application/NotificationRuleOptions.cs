namespace BudsMonitor.Application;

/// <summary>Inputs that drive <see cref="NotificationRuleService"/>, derived from user settings.</summary>
public sealed record NotificationRuleOptions
{
    public bool Enabled { get; init; } = true;
    public int EarbudThresholdPercent { get; init; } = 20;
    public int CaseThresholdPercent { get; init; } = 20;
    public TimeSpan SuppressWindow { get; init; } = TimeSpan.FromMinutes(60);
    public bool NotifyFromStaleData { get; init; }
    public TimeSpan StaleAfter { get; init; } = TimeSpan.FromSeconds(120);
    public bool QuietHoursEnabled { get; init; }
    public TimeOnly QuietHoursStart { get; init; } = new(22, 0);
    public TimeOnly QuietHoursEnd { get; init; } = new(7, 0);
}
