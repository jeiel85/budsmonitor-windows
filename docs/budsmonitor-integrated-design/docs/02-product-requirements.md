# 02. Product Requirements

## Product statement

BudsMonitor is a Windows tray application that gives reliable, low-friction visibility into Bluetooth earbuds battery and status, especially AirPods and Galaxy Buds.

## Target user

A Windows laptop user who owns multiple Bluetooth earbuds and wants one reliable local app instead of paying for a battery monitor utility.

## Daily-driver requirements

The app must be good enough to remain enabled at startup and run for a full workday.

### Functional requirements

| ID | Requirement | Priority |
|---|---|---|
| FR-001 | Show tray icon while running | Must |
| FR-002 | Start with Windows option | Must |
| FR-003 | Detect AirPods BLE proximity advertisements | Must |
| FR-004 | Parse AirPods left/right/case battery where available | Must |
| FR-005 | Show AirPods in-ear state where available | Should |
| FR-006 | Show stale state when live data is old | Must |
| FR-007 | Store last known battery snapshot | Must |
| FR-008 | Support generic BLE Battery Service devices | Must |
| FR-009 | Support low-battery notifications | Must |
| FR-010 | Let user hide irrelevant Bluetooth devices | Must |
| FR-011 | Let user pin favorite devices | Must |
| FR-012 | Let user assign device aliases | Should |
| FR-013 | Generate diagnostics export | Must |
| FR-014 | Provide Galaxy Buds provider track | Should for v1.0, Must for v1.x |
| FR-015 | Show local-only privacy status | Must |

### Non-functional requirements

| ID | Requirement | Target |
|---|---|---|
| NFR-001 | Startup time | Main tray visible within 3 seconds |
| NFR-002 | Idle CPU | Near-zero under normal idle scanning |
| NFR-003 | Memory stability | No visible leak over 8 hours |
| NFR-004 | Network usage | Zero network calls |
| NFR-005 | Crash handling | Logs error and restarts scanner where possible |
| NFR-006 | Privacy | Bluetooth address masked in diagnostics by default |
| NFR-007 | Installer | Extract-and-run first, installer later |
| NFR-008 | Windows support | Windows 10 1809+ minimum, Windows 11 primary |

## User stories

### US-001: Quick tray battery check

As a user, I want to hover over the tray icon or right-click it and see my AirPods battery immediately.

Acceptance:

- Tray tooltip displays a compact summary.
- Right-click menu displays each pinned/active device.
- Values indicate whether they are live or stale.

### US-002: Main dashboard

As a user, I want a clean dashboard showing all relevant earbuds and battery components.

Acceptance:

- AirPods card shows left/right/case where available.
- Generic devices show whole-device battery.
- Irrelevant devices are hidden by default or easy to hide.

### US-003: Bluetooth weirdness recovery

As a user, I do not want to restart the app every time Bluetooth sleeps or reconnects.

Acceptance:

- Scanner restarts after Windows resume.
- Watcher failure is surfaced and auto-recovered.
- Last known values remain visible with stale labels.

### US-004: Low battery alert

As a user, I want to know before a pod or case dies.

Acceptance:

- Notification fires below configured threshold.
- Repeated alerts are suppressed for a configurable interval.
- Charging or battery recovery resets the alert state.

### US-005: Diagnostics

As a developer-user, I want to generate a diagnostic bundle when a device is not working.

Acceptance:

- Export includes environment, devices, provider attempts, raw BLE payload hashes/samples where safe, and logs.
- Bluetooth addresses are masked by default.
- Export is local only.

## Product quality gate

Do not call v1.0 complete unless all of these are true:

```text
- AirPods BLE provider works on at least one real AirPods/AirPods Pro model.
- Generic GATT provider works on at least one Battery Service device.
- App survives Bluetooth off/on.
- App survives sleep/resume.
- Tray icon and menu remain responsive after 8 hours.
- Diagnostics export is generated successfully.
- No network call exists in normal operation.
```
