# 01. Existing Repository Integration Audit

Repository reviewed:

```text
jeiel85/librepods-windows-ble
```

## Executive verdict

The repository is highly reusable, but only for the BLE-only AirPods monitoring path.

It should not be used as the basis for a full Windows AACP control product because its own feasibility work closes that path for normal userspace applications.

## Reusable assets

### 1. AirPods BLE advertisement parser

Reusable value: very high.

Existing locations:

```text
experiments/windows-feasibility/battery-tray-mvp/Program.cs
linux/ble/bleadvertisementparser.h
linux/ble/bleadvertisementparser.cpp
linux/ble/bleinfo.h
```

Use in new app:

```text
src/BudsMonitor.Providers.AirPods/
  AirPodsAdvertisementParser.cs
  AirPodsAdvertisementSnapshot.cs
  AirPodsModelId.cs
  AirPodsBatteryMapper.cs
```

### 2. Windows BLE scanner behavior

Reusable value: high.

Existing locations:

```text
linux/platform/windows/windowsblescanner.h
linux/platform/windows/windowsblescanner.cpp
experiments/windows-feasibility/battery-tray-mvp/Program.cs
```

Use in new app:

```text
src/BudsMonitor.Bluetooth/
  BleAdvertisementScannerService.cs
```

The new version should use C# `BluetoothLEAdvertisementWatcher` directly.

### 3. Feasibility evidence

Reusable value: very high.

Existing locations:

```text
experiments/windows-feasibility/RESULTS.md
docs/windows-porting-progress.md
docs/wsl2-usbipd-guide.md
```

Use in new app:

```text
docs/research/windows-aacp-feasibility.md
docs/research/legacy-libre-pods-windows-porting.md
```

### 4. Diagnostic probes

Reusable value: medium-high.

Existing locations:

```text
linux/tests/windows_aacp_probe.cpp
linux/tests/windows_hci_ioctl_probe.cpp
linux/tests/windows_bredr_l2cap_probe.cpp
linux/tests/windows_ble_scanner_smoke.cpp
linux/tests/ble_parser_smoke.cpp
```

Use in new app:

- Keep as archived research tools.
- Do not ship all probes to normal users by default.
- Expose normal diagnostics through the new app's own UI.
- Advanced probes can remain in `tools/legacy-probes/`.

### 5. Tray MVP UX

Reusable value: medium.

Existing locations:

```text
linux/platform/windows/windowstraymain.cpp
linux/platform/windows/qml/Tray.qml
linux/platform/windows/qml/BatteryIndicator.qml
```

Use in new app:

- Use as behavioral reference only.
- Do not carry Qt/QML into the final WPF product.

## Assets not to carry forward

### 1. Full AACP control path

Do not continue work on normal userspace AACP control for v1.0.

The old repo already tested:

- WinRT high-level RFCOMM/GATT
- Native SDP
- Winsock L2CAP bind/connect
- HCI IOCTL
- Admin elevation
- BR/EDR forcing via `BluetoothSetServiceState`

The final evidence indicates standard userspace paths are blocked.

### 2. Linux app structure

The old fork keeps the upstream Linux layout. The new Windows product should not inherit that structure.

Avoid:

```text
linux/ as top-level app source for Windows
Qt/QML dependency as core architecture
Linux DBus/PulseAudio assumptions
```

### 3. Product naming as LibrePods Windows

The new app should not present itself as a full LibrePods Windows port because active controls are not supported. It should present itself as a Windows earbuds monitor.

## Recommended migration model

Use a **reference-port**, not a direct folder merge.

```text
Old repo                                  New repo
--------                                  --------
C# AirPods parser PoC            --->     BudsMonitor.Providers.AirPods
C++ BleInfo model                --->     Domain model mapping reference
C++ WindowsBleScanner            --->     C# scanner service reference
RESULTS.md                       --->     docs/research source
windows_*_probe.cpp              --->     tools/legacy-probes optional archive
Qt tray MVP                      --->     UX behavior reference only
```

## Legal note

Because the old repository is GPLv3-or-later, directly copying code into BudsMonitor means the new app should be GPLv3-or-later when distributed. For a private-only personal app this is not a practical blocker. For future public distribution, GPLv3 is the cleanest licensing path.
