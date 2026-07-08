namespace BudsMonitor.Application;

/// <summary>A notification the app should surface (mapped to a tray balloon / toast).</summary>
public sealed record NotificationRequest
{
    public required string DeviceKey { get; init; }
    public required string Title { get; init; }
    public required string Body { get; init; }
}
