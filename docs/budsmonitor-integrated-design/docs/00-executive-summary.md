# 00. Executive Summary

## Decision

Proceed with an integrated design that uses the previous `librepods-windows-ble` repository as the AirPods BLE evidence and implementation seed, but do not continue the old project as a full LibrePods Windows port.

The new app should be a focused Windows daily-driver tray utility:

```text
BudsMonitor for Windows
```

The production repository identity should be:

```text
jeiel85/budsmonitor-windows
```

Treat `jeiel85/librepods-windows-ble` as the legacy feasibility fork and source-evidence name. New implementation, release, and packaging work should use the BudsMonitor identity.

## Product identity

BudsMonitor is not a generic MVP battery reader. It is a production-quality, local-only Bluetooth earbuds monitoring app optimized for the devices the owner actually uses.

## Why the previous repository matters

The previous work already answered the hardest AirPods feasibility question:

- BLE advertisements can be received on Windows.
- AirPods proximity data can be parsed.
- Left/right/case battery can be obtained when the advertisement contains it.
- In-ear state can be obtained when the advertisement contains it.
- Normal Windows userspace cannot implement full AACP active control through raw L2CAP.

Therefore, the new product should treat AirPods BLE as a first-class provider rather than a later research item.

## Final v1.0 scope

### In scope

- Windows tray-first desktop app
- AirPods BLE advertisement provider
- AirPods battery left/right/case where available
- AirPods charging state and in-ear state where available
- Generic BLE Battery Service fallback
- Device cards with last-known values and stale indicators
- Low battery notifications
- Local settings and cache
- Diagnostics export
- Single-instance behavior
- Start with Windows option
- Local-only privacy policy

### In scope as research-backed extension

- Galaxy Buds provider
- Galaxy Buds model profiles
- Samsung-specific BLE/GATT/vendor behavior investigation

### Out of scope for v1.0

- AirPods ANC / transparency control
- Conversation awareness control
- Head gesture configuration
- Firmware management
- Custom kernel driver
- WSL2 USB dongle bridge as normal-user dependency
- Cloud sync
- Account system
- Analytics
- Ads

## Main architectural decision

Use **C# / .NET / WPF** for the final app shell, not Qt/QML.

Reason:

- The previous repo already has a C# feasibility implementation of the AirPods parser.
- WPF is pragmatic for tray-first utilities.
- The app needs Windows integration, settings, notifications, diagnostics, local cache, and user-friendly UX more than cross-platform Qt abstractions.
- Existing C++/Qt scanner code remains useful as evidence and reference, but the final product should be easier to maintain in the user's Windows/.NET workflow.

## Success definition

BudsMonitor v1.0 is successful only when it is good enough to keep running every day:

- It starts with Windows.
- It sits quietly in the tray.
- It detects the user's AirPods without manual steps.
- It shows useful battery information even when live data temporarily disappears.
- It clearly distinguishes live values from cached/stale values.
- It does not crash or spin CPU when Bluetooth behaves poorly.
- It never sends data to the network.
