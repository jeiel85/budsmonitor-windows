using BudsMonitor.Domain;

namespace BudsMonitor.Providers.Gatt;

/// <summary>Reads a device's standard Battery Service level. Abstracted so the provider is testable.</summary>
public interface IGattClient
{
    Task<GattBatteryReadOutcome> ReadBatteryLevelAsync(string windowsDeviceId, CancellationToken cancellationToken);
}

public enum GattReadKind
{
    Success,
    NotApplicable,
    Failure,
}

public sealed record GattBatteryReadOutcome
{
    public required GattReadKind Kind { get; init; }
    public int Percentage { get; init; }
    public BatteryReadFailureReason FailureReason { get; init; }

    public static GattBatteryReadOutcome Success(int percentage)
        => new() { Kind = GattReadKind.Success, Percentage = percentage };

    public static GattBatteryReadOutcome NotApplicable()
        => new() { Kind = GattReadKind.NotApplicable };

    public static GattBatteryReadOutcome Failure(BatteryReadFailureReason reason)
        => new() { Kind = GattReadKind.Failure, FailureReason = reason };
}
