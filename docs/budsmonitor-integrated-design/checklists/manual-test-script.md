# Manual Test Script

## AirPods live test

1. Start BudsMonitor.
2. Open AirPods case near laptop.
3. Wait up to 30 seconds.
4. Confirm dashboard shows AirPods card.
5. Confirm tray tooltip has battery summary.
6. Put one pod in ear.
7. Confirm in-ear state changes if payload provides it.
8. Close case and wait 2 minutes.
9. Confirm stale state appears.

## Generic GATT test

1. Pair a BLE device that exposes Battery Service.
2. Open dashboard.
3. Click Refresh.
4. Confirm whole-device battery appears.
5. Disconnect device.
6. Confirm disconnected or last-known state appears.

## Bluetooth off/on

1. Start app.
2. Turn Bluetooth off in Windows.
3. Confirm app does not crash.
4. Confirm Bluetooth-off UI state.
5. Turn Bluetooth on.
6. Confirm scanner restarts or can be restarted.

## Sleep/resume

1. Start app with AirPods seen.
2. Put laptop to sleep.
3. Resume after 5 minutes.
4. Open AirPods case.
5. Confirm live data returns without app restart.

## Diagnostics

1. Open Diagnostics window.
2. Generate diagnostics.
3. Open ZIP.
4. Confirm required files exist.
5. Confirm Bluetooth addresses are masked.
