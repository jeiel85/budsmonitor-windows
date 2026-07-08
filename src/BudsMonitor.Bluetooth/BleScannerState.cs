namespace BudsMonitor.Bluetooth;

/// <summary>Lifecycle state of the BLE advertisement scanner, surfaced to the app.</summary>
public enum BleScannerState
{
    Idle,
    Running,
    Stopped,
    Failed,
}
