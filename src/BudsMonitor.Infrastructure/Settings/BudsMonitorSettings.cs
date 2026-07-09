namespace BudsMonitor.Infrastructure.Settings;

/// <summary>
/// Persisted application settings. Defaults match docs/09-storage-privacy and
/// specs/settings.schema.json. Stored at %AppData%\BudsMonitor\settings.json.
/// </summary>
public sealed record BudsMonitorSettings
{
    public int Version { get; init; } = 1;
    public AppSettings App { get; init; } = new();
    public MonitoringSettings Monitoring { get; init; } = new();
    public NotificationSettings Notifications { get; init; } = new();
    public PrivacySettings Privacy { get; init; } = new();
    public UpdatesSettings Updates { get; init; } = new();
}

public sealed record UpdatesSettings
{
    /// <summary>Check GitHub for a newer release shortly after launch. The only network use.</summary>
    public bool CheckOnStartup { get; init; } = true;

    /// <summary>A version the user chose to skip; the same version won't prompt again.</summary>
    public string SkippedVersion { get; init; } = "";
}

public sealed record AppSettings
{
    public bool StartWithWindows { get; init; } = true;
    public bool MinimizeToTray { get; init; } = true;
    public string Theme { get; init; } = "system";
    public string Language { get; init; } = "ko-KR";
}

public sealed record MonitoringSettings
{
    public bool EnableAirPodsBleScanner { get; init; } = true;
    public bool EnableGenericGattRefresh { get; init; } = true;
    public int GenericGattPollingIntervalSeconds { get; init; } = 300;
    public int StaleAfterSeconds { get; init; } = 120;
    public int LastKnownVisibleForHours { get; init; } = 24;
}

public sealed record NotificationSettings
{
    public bool Enabled { get; init; } = true;
    public int EarbudLowBatteryThreshold { get; init; } = 20;
    public int CaseLowBatteryThreshold { get; init; } = 20;
    public int SuppressRepeatedMinutes { get; init; } = 60;
    public bool NotifyFromStaleData { get; init; } = false;
    public bool QuietHoursEnabled { get; init; } = false;
    public string QuietHoursStart { get; init; } = "22:00";
    public string QuietHoursEnd { get; init; } = "07:00";
}

public sealed record PrivacySettings
{
    public bool MaskBluetoothAddressesInLogs { get; init; } = true;
    public bool IncludeRawPayloadsInDiagnostics { get; init; } = false;

    // Hard local-only rules — always false, enforced on load/save.
    public bool AnalyticsEnabled { get; init; } = false;
    public bool NetworkEnabled { get; init; } = false;
}
