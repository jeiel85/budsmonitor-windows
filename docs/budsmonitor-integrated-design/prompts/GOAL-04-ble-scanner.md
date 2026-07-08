# Codex Prompt — GOAL 4 BLE Advertisement Scanner

Implement the BLE advertisement scanner service for BudsMonitor.

## Tasks

1. Create `BudsMonitor.Bluetooth/BleAdvertisementScannerService.cs`.
2. Use `BluetoothLEAdvertisementWatcher`.
3. Use active scanning.
4. Convert each manufacturer section into a `BleAdvertisementFrame`.
5. Publish frames to `Channel<BleAdvertisementFrame>` or an injected event sink.
6. Handle `Stopped` event and expose scanner status.
7. Add `StartAsync`, `StopAsync`, and `RestartAsync`.
8. Do not mutate WPF view models directly from the watcher callback.

## Acceptance

- Scanner starts and stops cleanly.
- Scanner can be restarted from tray menu.
- Apple manufacturer frames can reach AirPods provider.
- Errors are logged and surfaced as scanner state.
