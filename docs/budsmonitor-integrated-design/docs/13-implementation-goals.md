# 13. Implementation Goals

These goals are sized for coding agents. Complete in order.

## GOAL 0 — Repository foundation

### Objective

Create the new solution skeleton and preserve the old repo evidence.

### Tasks

```text
- Create BudsMonitor.sln.
- Add project folders.
- Add README, LICENSE, docs/research placeholders.
- Copy or summarize old feasibility docs into docs/research.
- Add initial .gitignore.
```

### Done when

```text
- Solution opens.
- Empty app builds.
- Documentation includes old repo integration note.
```

## GOAL 1 — WPF tray shell

### Objective

Create the daily-driver app shell.

### Tasks

```text
- Create WPF app.
- Add NotifyIcon tray integration.
- Add single-instance guard.
- Add main dashboard window.
- Add Settings window placeholder.
- Add Quit behavior.
- Make close button minimize to tray.
```

### Done when

```text
- App launches.
- Tray icon appears.
- Right-click menu works.
- Main window opens from tray.
- Quit fully exits.
```

## GOAL 2 — Settings, logging, cache foundation

### Tasks

```text
- Add settings.json repository.
- Add battery-cache.json repository.
- Add Serilog file logging.
- Add log retention.
- Add local-only privacy defaults.
```

### Done when

```text
- Settings file auto-created.
- Logs written to LocalAppData.
- Cache save/load tested.
```

## GOAL 3 — AirPods parser port

### Tasks

```text
- Port C# AirPodsAdvertisementParser from old feasibility MVP.
- Create AirPodsAdvertisementSnapshot.
- Create model id catalog.
- Add parser unit tests.
- Add sample payload fixtures if available.
```

### Done when

```text
- Parser tests pass.
- Invalid payloads do not throw.
- Known model ids map correctly.
```

## GOAL 4 — BLE advertisement scanner

### Tasks

```text
- Implement BluetoothLEAdvertisementWatcher wrapper.
- Publish BleAdvertisementFrame events through Channel.
- Filter Apple Company ID for AirPods provider.
- Add scanner start/stop/restart.
- Add stopped/error handling.
```

### Done when

```text
- App can log received Apple manufacturer frames.
- Scanner can restart from UI.
- Scanner failure is visible in app state.
```

## GOAL 5 — AirPods provider integration

### Tasks

```text
- Add AirPodsBleAdvertisementProvider.
- Convert AirPods snapshot to BatterySnapshot.
- Update battery cache.
- Update dashboard device card.
- Update tray tooltip/menu.
- Add stale timer.
```

### Done when

```text
- Real AirPods produce visible left/right/case where available.
- Last known values remain after packets stop.
- UI marks stale values correctly.
```

## GOAL 6 — Notification engine

### Tasks

```text
- Add notification settings.
- Add threshold rules.
- Add suppression window.
- Add stale-data rule.
- Add Windows toast or tray balloon fallback.
```

### Done when

```text
- Low battery event triggers one notification.
- Repeated event is suppressed.
- Stale cache does not notify by default.
```

## GOAL 7 — Generic GATT provider

### Tasks

```text
- Add device enumeration for paired BLE devices.
- Add StandardGattBatteryProvider.
- Add GATT timeout/retry policy.
- Add UI support for whole-device battery.
```

### Done when

```text
- At least one standard Battery Service device shows battery.
- Devices without Battery Service show clean unsupported state.
```

## GOAL 8 — Device management UX

### Tasks

```text
- Add pin/hide/alias controls.
- Add device registry persistence.
- Filter irrelevant devices.
- Add show hidden devices option.
```

### Done when

```text
- User can hide noise devices.
- Pinned devices remain in dashboard.
- Aliases persist.
```

## GOAL 9 — Diagnostics export

### Tasks

```text
- Add diagnostics window.
- Capture environment.
- Capture provider attempts.
- Capture redacted settings/cache.
- Capture optional advertisement and GATT data.
- Export ZIP.
```

### Done when

```text
- Diagnostics ZIP generated.
- Bluetooth addresses masked by default.
- Export contains enough data to debug provider failures.
```

## GOAL 10 — Sleep/resume and self-repair

### Tasks

```text
- Detect system resume.
- Restart scanner after resume.
- Backoff repeated provider failures.
- Handle Bluetooth off/on state.
```

### Done when

```text
- App recovers from sleep/resume.
- App recovers from Bluetooth off/on.
```

## GOAL 11 — Galaxy Buds diagnostics track

### Tasks

```text
- Add Galaxy Buds classifier.
- Add profile JSON files.
- Add diagnostics capture path for Galaxy Buds.
- Try standard GATT fallback.
```

### Done when

```text
- Galaxy Buds candidate appears as limited support.
- Diagnostic bundle can be generated for it.
```

## GOAL 12 — Daily-driver beta hardening

### Tasks

```text
- 8-hour run test.
- Sleep/resume test.
- Bluetooth off/on test.
- No-network audit.
- Package portable ZIP.
- Write troubleshooting docs.
```

### Done when

```text
- App is usable daily on the owner's laptop.
- Known limitations are documented honestly.
```
