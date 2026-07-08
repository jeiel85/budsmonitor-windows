using BudsMonitor.Domain;

namespace BudsMonitor.Providers.Gatt;

/// <summary>
/// Reads battery from any device exposing the standard Battery Service via GATT.
/// Per-read timeout with a single retry for transient failures (see docs/07).
/// </summary>
public sealed class StandardGattBatteryProvider : IBatteryProvider
{
    private readonly IGattClient _client;
    private readonly TimeSpan _readTimeout;
    private readonly TimeSpan _retryDelay;

    public StandardGattBatteryProvider(
        IGattClient? client = null,
        TimeSpan? readTimeout = null,
        TimeSpan? retryDelay = null)
    {
        _client = client ?? new WindowsGattClient();
        _readTimeout = readTimeout ?? TimeSpan.FromSeconds(10);
        _retryDelay = retryDelay ?? TimeSpan.FromSeconds(3);
    }

    public string ProviderId => "standard-gatt-battery";
    public ProviderKind Kind => ProviderKind.Gatt;

    public async Task<ProviderProbeResult> ProbeAsync(DeviceCandidate device, CancellationToken cancellationToken)
    {
        var outcome = await ReadWithRetryAsync(device, cancellationToken);
        return outcome.Kind switch
        {
            GattReadKind.Success => new ProviderProbeResult(ProviderId, true, null, null),
            GattReadKind.NotApplicable =>
                new ProviderProbeResult(ProviderId, false, BatteryReadFailureReason.BatteryServiceNotFound, "No Battery Service"),
            _ => new ProviderProbeResult(ProviderId, false, outcome.FailureReason, "GATT read failed"),
        };
    }

    public async Task<BatteryReadResult> ReadAsync(DeviceCandidate device, CancellationToken cancellationToken)
    {
        var outcome = await ReadWithRetryAsync(device, cancellationToken);
        return outcome.Kind switch
        {
            GattReadKind.Success => BatteryReadResult.Success(ProviderId, ToSnapshot(device, outcome.Percentage)),
            GattReadKind.NotApplicable => BatteryReadResult.NotApplicable(ProviderId),
            _ => BatteryReadResult.Failed(ProviderId, outcome.FailureReason, "GATT read failed"),
        };
    }

    private async Task<GattBatteryReadOutcome> ReadWithRetryAsync(DeviceCandidate device, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(device.WindowsDeviceId))
        {
            return GattBatteryReadOutcome.Failure(BatteryReadFailureReason.DeviceNotConnected);
        }

        var first = await ReadOnceAsync(device.WindowsDeviceId, cancellationToken);
        if (first.Kind != GattReadKind.Failure)
        {
            return first;
        }

        // One retry after a delay, only for transient failures.
        var transient = first.FailureReason is BatteryReadFailureReason.GattTimeout
            or BatteryReadFailureReason.DeviceNotConnected;
        if (!transient)
        {
            return first;
        }

        try
        {
            await Task.Delay(_retryDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return first;
        }

        return await ReadOnceAsync(device.WindowsDeviceId, cancellationToken);
    }

    private async Task<GattBatteryReadOutcome> ReadOnceAsync(string windowsDeviceId, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(_readTimeout);
        return await _client.ReadBatteryLevelAsync(windowsDeviceId, cts.Token);
    }

    private static BatterySnapshot ToSnapshot(DeviceCandidate device, int percentage) => new()
    {
        StableDeviceKey = device.StableDeviceKey,
        DisplayName = device.DisplayName,
        DeviceKind = device.Kind,
        Components =
        [
            new BatteryComponent
            {
                Type = BatteryComponentType.WholeDevice,
                Percentage = percentage,
                IsCharging = null,
                Label = "Battery",
            },
        ],
        Source = BatteryDataSource.StandardGattBatteryService,
        Confidence = BatteryConfidence.Medium,
        MeasuredAt = DateTimeOffset.Now,
        ExpectedFreshness = TimeSpan.FromMinutes(5),
    };
}
