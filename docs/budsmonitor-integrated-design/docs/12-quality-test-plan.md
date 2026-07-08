# 12. Quality and Test Plan

## Test pyramid

```text
Parser unit tests
Provider unit tests
Application service tests
UI view-model tests
Manual real-device tests
Long-run stability tests
```

## Unit tests

### AirPods parser tests

| Test | Expected |
|---|---|
| Too-short payload | Reject |
| Non-AirPods Apple payload | Reject |
| Known model id | Correct model name |
| Unknown model id | `Unknown` but not crash |
| Battery nibble 15 | unavailable |
| Battery nibble 0 | 0% |
| Battery nibble 10 | 100% |
| Primary left flag | left/right mapped correctly |
| Charging flags | mapped correctly |

### GATT provider tests

Mock `IWindowsGattClient`.

| Test | Expected |
|---|---|
| Battery service missing | NotApplicable |
| Characteristic missing | Failure reason set |
| Empty buffer | Invalid payload |
| Value 101 | Invalid payload |
| Timeout | GattTimeout |
| Success value | BatterySnapshot |

### Cache tests

| Test | Expected |
|---|---|
| Save snapshot | File written |
| Load snapshot | Same values |
| Stale calculation | Correct state |
| Corrupt file | Recovered with backup/log |

### Notification tests

| Test | Expected |
|---|---|
| Below threshold | Notify |
| Above threshold | Do not notify |
| Suppression window | Do not repeat |
| Stale data | Do not notify by default |
| Charging recovery | Reset alert state |

## Manual device test matrix

| Device | Required before v1.0 |
|---|---|
| AirPods or AirPods Pro | Yes |
| One generic BLE Battery Service device | Yes |
| Galaxy Buds | Recommended; required before declaring support |
| Bluetooth radio off/on | Yes |
| Sleep/resume | Yes |
| No Bluetooth device paired | Yes |

## Long-run tests

### 8-hour run

Procedure:

```text
1. Start app.
2. Connect AirPods.
3. Confirm live data.
4. Keep app running 8 hours.
5. Use PC normally.
6. Verify tray responsive.
7. Verify memory stable.
8. Verify logs not exploding.
```

Acceptance:

```text
- No crash.
- No UI hang.
- Memory growth is bounded.
- CPU stays low.
- Log file stays under retention/rotation threshold.
```

### Sleep/resume test

Procedure:

```text
1. Start app.
2. Connect earbuds.
3. Confirm battery visible.
4. Put laptop to sleep.
5. Resume after 5 minutes.
6. Open earbuds case or reconnect.
7. Verify app recovers without restart.
```

Acceptance:

```text
- Scanner restarts if needed.
- UI shows stale state during gap.
- Live data returns when advertisements resume.
```

### Bluetooth off/on test

Acceptance:

```text
- App does not crash.
- UI shows Bluetooth off.
- Scanner restarts after Bluetooth returns.
```

## Release blocker list

Do not release if any are true:

```text
- App crashes on startup.
- Tray icon remains after exit as ghost icon repeatedly.
- AirPods parser throws on malformed payload.
- Scanner stopped state is not recoverable.
- Bluetooth off crashes provider code.
- Network calls exist in normal app code.
- Diagnostics exports raw addresses by default.
- Logs grow without limit.
```
