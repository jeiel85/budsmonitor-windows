# Windows Platform Backend

This folder contains Windows-specific backend code for the Qt/C++ port.

Current status:

- `WindowsBleScanner` wraps C++/WinRT `BluetoothLEAdvertisementWatcher`.
- It emits the shared `IBleScanner::deviceFound(BleInfo)` signal after parsing Apple manufacturer data with `BleAdvertisementParser`.
- Emission is queued back onto the Qt object thread because WinRT advertisement callbacks can arrive on non-Qt threads.

Next backend candidates:

- Windows tray/QML app entrypoint
- Core Audio volume endpoint controller
- GSMTC media session controller
- Native Bluetooth SDP/socket probe for AACP
