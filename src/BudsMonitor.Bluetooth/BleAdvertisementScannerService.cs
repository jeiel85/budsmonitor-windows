using System.Threading.Channels;
using BudsMonitor.Domain;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace BudsMonitor.Bluetooth;

/// <summary>
/// Wraps a <see cref="BluetoothLEAdvertisementWatcher"/> and republishes each
/// manufacturer-data section as a <see cref="BleAdvertisementFrame"/> through a bounded
/// channel. Watcher callbacks run on a WinRT thread; consumers read from
/// <see cref="Frames"/> and marshal to the UI themselves (see architecture threading rules).
/// </summary>
public sealed class BleAdvertisementScannerService : IDisposable
{
    private readonly BluetoothLEAdvertisementWatcher _watcher;
    private readonly Channel<BleAdvertisementFrame> _channel;

    public BleAdvertisementScannerService()
    {
        _watcher = new BluetoothLEAdvertisementWatcher
        {
            ScanningMode = BluetoothLEScanningMode.Active,
        };
        _watcher.Received += OnReceived;
        _watcher.Stopped += OnStopped;

        // Advertisements arrive faster than the UI consumes them; keep only the freshest.
        _channel = Channel.CreateBounded<BleAdvertisementFrame>(
            new BoundedChannelOptions(1024)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
                SingleWriter = false,
            });
    }

    /// <summary>Stream of received advertisement frames (all company ids; filter downstream).</summary>
    public ChannelReader<BleAdvertisementFrame> Frames => _channel.Reader;

    public BleScannerState State { get; private set; } = BleScannerState.Idle;
    public BluetoothError? LastError { get; private set; }

    public event EventHandler<BleScannerState>? StateChanged;

    public Task StartAsync()
    {
        if (_watcher.Status != BluetoothLEAdvertisementWatcherStatus.Started)
        {
            _watcher.Start();
        }

        SetState(BleScannerState.Running);
        return Task.CompletedTask;
    }

    public Task StopAsync()
    {
        if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
        {
            _watcher.Stop();
        }

        SetState(BleScannerState.Stopped);
        return Task.CompletedTask;
    }

    public async Task RestartAsync()
    {
        await StopAsync();
        await StartAsync();
    }

    private void OnReceived(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementReceivedEventArgs args)
    {
        foreach (var section in args.Advertisement.ManufacturerData)
        {
            var frame = new BleAdvertisementFrame
            {
                ReceivedAt = args.Timestamp,
                CompanyId = section.CompanyId,
                ManufacturerData = ReadBuffer(section.Data),
                BluetoothAddress = args.BluetoothAddress,
                LocalName = string.IsNullOrEmpty(args.Advertisement.LocalName)
                    ? null
                    : args.Advertisement.LocalName,
                RawRssi = (short)args.RawSignalStrengthInDBm,
            };

            _channel.Writer.TryWrite(frame);
        }
    }

    private void OnStopped(
        BluetoothLEAdvertisementWatcher sender,
        BluetoothLEAdvertisementWatcherStoppedEventArgs args)
    {
        if (args.Error is BluetoothError.Success)
        {
            SetState(BleScannerState.Stopped);
        }
        else
        {
            SetState(BleScannerState.Failed, args.Error);
        }
    }

    private void SetState(BleScannerState state, BluetoothError? error = null)
    {
        State = state;
        LastError = error;
        StateChanged?.Invoke(this, state);
    }

    private static byte[] ReadBuffer(IBuffer buffer)
    {
        var data = new byte[buffer.Length];
        using var reader = DataReader.FromBuffer(buffer);
        reader.ReadBytes(data);
        return data;
    }

    public void Dispose()
    {
        _watcher.Received -= OnReceived;
        _watcher.Stopped -= OnStopped;

        if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
        {
            _watcher.Stop();
        }

        _channel.Writer.TryComplete();
    }
}
