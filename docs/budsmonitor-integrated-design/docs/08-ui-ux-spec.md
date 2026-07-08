# 08. UI / UX Specification

## UX principle

The app must never leave the user wondering whether a value is live, old, unsupported, or broken.

## Main surfaces

1. Tray icon
2. Tray right-click menu
3. Popover / compact window
4. Full settings window
5. Diagnostics window

## Tray tooltip

Examples:

```text
AirPods Pro 2 · L 80 R 90 Case 60 · live
```

If stale:

```text
AirPods Pro 2 · L 80 R 90 Case 60 · 12m old
```

If no device:

```text
BudsMonitor · no earbuds detected
```

## Tray menu

```text
BudsMonitor
────────────────────────
AirPods Pro 2
L 80% · R 90% · Case 60% · live

Galaxy Buds2 Pro
Battery 70% · limited support
────────────────────────
Refresh now
Open dashboard
Diagnostics
Settings
Quit
```

## Dashboard layout

```text
┌────────────────────────────────────────────┐
│ BudsMonitor                           ⚙  _ │
├────────────────────────────────────────────┤
│ AirPods Pro 2                              │
│ Connected · Live · Updated 13:42           │
│                                            │
│ Left       80%  charging: no               │
│ Right      90%  charging: no               │
│ Case       60%  charging: yes              │
│                                            │
│ In ear: Left yes · Right yes               │
├────────────────────────────────────────────┤
│ Galaxy Buds2 Pro                           │
│ Connected · Limited support                │
│ Battery    70%                             │
│ [Generate diagnostics]                     │
└────────────────────────────────────────────┘
```

## Device card states

| State | Visual behavior |
|---|---|
| Live | Normal text, live badge |
| Recently seen | Normal text, subtle age badge |
| Stale | Dimmed values, `old data` badge |
| Unsupported | No battery bars; show action button |
| Error | Clear reason and retry action |
| Hidden | Not displayed unless settings page opens |

## Stale value display

Examples:

```text
Last known 12 minutes ago
```

```text
Battery data is old. Open the case or reconnect the earbuds to update.
```

## AirPods-specific UX

AirPods card may show:

- left battery
- right battery
- case battery
- charging state
- in-ear state
- model
- BLE freshness

Do not show unsupported active controls.

Bad UX:

```text
ANC: unavailable
Transparency: unavailable
```

Good UX:

Do not render active-control sections at all in v1.0.

## Galaxy Buds-specific UX

Until detailed Galaxy Buds support is validated:

```text
Galaxy Buds2 Pro
Limited support

Standard battery: 70%
Detailed left/right/case data is not available yet.
[Generate diagnostics]
```

## Settings

Sections:

```text
General
- Start with Windows
- Minimize to tray
- Theme: System / Light / Dark

Devices
- Show hidden devices
- Pin/unpin
- Alias
- Hide device

Notifications
- Enable low battery notifications
- Earbuds threshold
- Case threshold
- Quiet hours
- Repeat suppression

Privacy
- Network access: disabled
- Analytics: disabled
- Mask Bluetooth addresses in diagnostics

Diagnostics
- Generate diagnostic bundle
- Open logs folder
```

## Notifications

Notification rules:

- Left/right bud below 20%: notify.
- Case below 20%: notify.
- Whole device below 20%: notify.
- Do not notify repeatedly within suppression window.
- Do not notify from stale data unless setting is enabled.

Notification text:

```text
AirPods Pro 2 battery is low
Left 10%, Right 70%, Case 60%
```

## Empty state

```text
No earbuds detected yet.

Open your earbuds case or connect a Bluetooth audio device.
[Open Windows Bluetooth settings]
[Run diagnostics]
```

## Error state examples

### Bluetooth off

```text
Bluetooth is turned off.
Turn on Bluetooth in Windows settings to monitor devices.
[Open Bluetooth settings]
```

### Scanner stopped

```text
BLE scanner stopped unexpectedly.
BudsMonitor will try to restart it automatically.
[Restart now]
```

### Provider unavailable

```text
Battery data is unavailable for this device.
This device did not expose a supported battery source.
[Generate diagnostics]
```
