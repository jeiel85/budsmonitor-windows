using BudsMonitor.Domain;
using BudsMonitor.Providers.Gatt;

namespace BudsMonitor.Tests;

public sealed class StandardGattBatteryProviderTests
{
    private sealed class FakeGattClient : IGattClient
    {
        private readonly Queue<GattBatteryReadOutcome> _outcomes;

        public FakeGattClient(params GattBatteryReadOutcome[] outcomes)
            => _outcomes = new Queue<GattBatteryReadOutcome>(outcomes);

        public int Calls { get; private set; }

        public Task<GattBatteryReadOutcome> ReadBatteryLevelAsync(string windowsDeviceId, CancellationToken cancellationToken)
        {
            Calls++;
            var outcome = _outcomes.Count > 0
                ? _outcomes.Dequeue()
                : GattBatteryReadOutcome.Failure(BatteryReadFailureReason.UnknownError);
            return Task.FromResult(outcome);
        }
    }

    private static DeviceCandidate Candidate() => new()
    {
        StableDeviceKey = "sha256:mouse",
        DisplayName = "BLE Mouse",
        Kind = DeviceKind.Mouse,
        WindowsDeviceId = "BT#test",
    };

    private static StandardGattBatteryProvider Provider(IGattClient client)
        => new(client, readTimeout: TimeSpan.FromSeconds(1), retryDelay: TimeSpan.Zero);

    [Fact]
    public async Task Read_Success_MapsWholeDeviceSnapshot()
    {
        var result = await Provider(new FakeGattClient(GattBatteryReadOutcome.Success(75)))
            .ReadAsync(Candidate(), CancellationToken.None);

        Assert.Equal(BatteryReadStatus.Success, result.Status);
        var component = Assert.Single(result.Snapshot!.Components);
        Assert.Equal(BatteryComponentType.WholeDevice, component.Type);
        Assert.Equal(75, component.Percentage);
        Assert.Equal(BatteryDataSource.StandardGattBatteryService, result.Snapshot.Source);
    }

    [Fact]
    public async Task Read_NoBatteryService_ReturnsNotApplicable()
    {
        var result = await Provider(new FakeGattClient(GattBatteryReadOutcome.NotApplicable()))
            .ReadAsync(Candidate(), CancellationToken.None);

        Assert.Equal(BatteryReadStatus.NotApplicable, result.Status);
        Assert.Null(result.Snapshot);
    }

    [Fact]
    public async Task Read_TransientFailure_RetriesOnceThenSucceeds()
    {
        var client = new FakeGattClient(
            GattBatteryReadOutcome.Failure(BatteryReadFailureReason.DeviceNotConnected),
            GattBatteryReadOutcome.Success(50));

        var result = await Provider(client).ReadAsync(Candidate(), CancellationToken.None);

        Assert.Equal(BatteryReadStatus.Success, result.Status);
        Assert.Equal(2, client.Calls);
    }

    [Fact]
    public async Task Read_NonTransientFailure_DoesNotRetry()
    {
        var client = new FakeGattClient(GattBatteryReadOutcome.Failure(BatteryReadFailureReason.GattAccessDenied));

        var result = await Provider(client).ReadAsync(Candidate(), CancellationToken.None);

        Assert.Equal(BatteryReadStatus.Failed, result.Status);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Read_MissingDeviceId_Fails()
    {
        var result = await Provider(new FakeGattClient())
            .ReadAsync(Candidate() with { WindowsDeviceId = null }, CancellationToken.None);

        Assert.Equal(BatteryReadStatus.Failed, result.Status);
    }

    [Fact]
    public async Task Probe_Success_CanHandle()
    {
        var probe = await Provider(new FakeGattClient(GattBatteryReadOutcome.Success(30)))
            .ProbeAsync(Candidate(), CancellationToken.None);

        Assert.True(probe.CanHandle);
    }
}
