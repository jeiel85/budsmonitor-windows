using Windows.Devices.Radios;

namespace BudsMonitor.Bluetooth;

/// <summary>
/// Watches the system Bluetooth radio and raises <see cref="StateChanged"/> with true when it
/// turns on and false when it turns off. The app uses this to self-repair the advertisement
/// scanner across Bluetooth toggles. Radio access can be unavailable (no adapter / access
/// denied); in that case the watcher stays inert rather than throwing.
/// </summary>
public sealed class BluetoothRadioWatcher : IDisposable
{
    private Radio? _radio;

    /// <summary>Current radio state, or null if no Bluetooth radio was found.</summary>
    public bool? IsRadioOn => _radio is null ? null : _radio.State == RadioState.On;

    /// <summary>Raised with true (on) or false (off/disabled) when the Bluetooth radio changes.</summary>
    public event EventHandler<bool>? StateChanged;

    public async Task StartAsync()
    {
        try
        {
            var radios = await Radio.GetRadiosAsync().AsTask();
            _radio = radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth);
            if (_radio is not null)
            {
                _radio.StateChanged += OnRadioStateChanged;
            }
        }
        catch
        {
            // No adapter or access denied: leave the watcher inert.
            _radio = null;
        }
    }

    private void OnRadioStateChanged(Radio sender, object args)
        => StateChanged?.Invoke(this, sender.State == RadioState.On);

    public void Dispose()
    {
        if (_radio is not null)
        {
            _radio.StateChanged -= OnRadioStateChanged;
            _radio = null;
        }
    }
}
