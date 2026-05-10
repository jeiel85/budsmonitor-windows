# Windows Platform Backend

This folder contains Windows-specific backend code for the Qt/C++ port.

Current status:

- `WindowsBleScanner` wraps C++/WinRT `BluetoothLEAdvertisementWatcher`.
- It emits the shared `IBleScanner::deviceFound(BleInfo)` signal after parsing Apple manufacturer data with `BleAdvertisementParser`.
- Emission is queued back onto the Qt object thread because WinRT advertisement callbacks can arrive on non-Qt threads.
- `windowstraymain.cpp` is the Windows tray app entrypoint. It owns a `QSystemTrayIcon` and updates tooltip/context menu from the BLE scanner. Built as the `librepods-windows-tray-mvp` target.
- `windowsairpodsstate.h` is the Windows-side QML-facing state. Property names mirror Linux `Battery`/`DeviceInfo` so the QML surface can converge later.
- `qml/Tray.qml` is a frameless tool-window popover bound to `airPodsState` (the `WindowsAirPodsState` instance). Tray-icon click toggles the popover positioned near the tray icon.

Next backend candidates:

- Core Audio volume endpoint controller
- GSMTC media session controller
- Native Bluetooth SDP/socket probe for AACP
