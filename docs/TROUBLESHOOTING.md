# BudsMonitor Troubleshooting & Known Limitations

BudsMonitor is a **local-only** Windows tray app. It never makes network calls during
normal operation (see [No-network guarantee](#no-network-guarantee)). When something looks
wrong, the fastest path to a diagnosis is the built-in diagnostics bundle.

## Where things live

| What | Path |
| --- | --- |
| Settings | `%AppData%\BudsMonitor\settings.json` |
| Device registry (alias/pin/hide) | `%AppData%\BudsMonitor\devices.json` |
| Battery cache (last-known) | `%LocalAppData%\BudsMonitor\cache\battery-cache.json` |
| Logs (14-day rolling) | `%LocalAppData%\BudsMonitor\logs\` |
| Diagnostics bundles | `%LocalAppData%\BudsMonitor\diagnostics\` |

## Generate a diagnostics bundle

Tray → **진단** (or Settings → 진단 → **진단 창 열기**) → **진단 번들 생성 (ZIP)**.

The ZIP lands in `%LocalAppData%\BudsMonitor\diagnostics\` and is **never uploaded**. By
default Bluetooth addresses are masked (`AA:BB:CC:**:**:**`) and raw advertisement payloads
are omitted. To include more detail for deep debugging, toggle the Privacy options in
`settings.json` (`maskBluetoothAddressesInLogs`, `includeRawPayloadsInDiagnostics`) and
regenerate. Machine and user names are never included.

The bundle contains `environment.json`, redacted `settings.json` / `devices.json` /
`battery-cache.json`, `scanner.json`, `provider-attempts.json`, `advertisement-samples.json`,
and the two most recent log files.

## Common symptoms

### No devices appear
- Make sure Bluetooth is on and the earbuds are powered.
- AirPods only broadcast battery over BLE proximity advertisements when the **case lid is
  open** (or the buds are actively pairing/in use nearby). Closed case → no advertisements.
- Click tray → **지금 새로 고침** to restart the scanner.
- Confirm the scanner is running: the tray tooltip shows `스캔 중`.

### Battery values look stale or stuck
- The freshness badge shows the data age: `실시간` (<30 s), `방금` (<2 min), `오래됨`
  (<15 min), `마지막 값` (cache). A dimmed card = stale.
- Bring the earbuds closer; advertisement range is short and walls/interference reduce it.
- Click **지금 새로 고침**.

### Only one earbud (L or R) shows
- AirPods advertise from the *primary* bud only; which side is primary can change. Seeing a
  single side is expected, not a bug. Toggling the buds (case open/close) can switch it.

### Values didn't update after sleep or Bluetooth toggle
- BudsMonitor auto-recovers on resume and on Bluetooth on/off (it restarts the scanner and
  re-polls). Recovery can take one poll cycle; if a device doesn't reappear, click
  **지금 새로 고침**.

### A device I don't care about clutters the list
- Right-click the card → **숨기기**. Show hidden devices again from Settings → 기기 →
  **숨긴 기기 표시**. Pin the ones you care about with **고정 / 해제**.

## Known limitations (honest)

- **AirPods active control is not possible.** Windows userspace cannot open the AirPods
  Classic-Bluetooth L2CAP control channel, so ANC/transparency, in-ear config, rename, and
  firmware are out of scope. BudsMonitor is battery/status monitoring only. This was
  confirmed by the earlier feasibility work.
- **AirPods auto-reconnect is not implemented.** If a nearby device's connect/disconnect
  drops your AirPods (e.g. crowded-office interference), BudsMonitor cannot force a
  userspace reconnect. Reconnect from Windows Bluetooth settings. (Other Windows AirPods
  tools have the same limit.)
- **Galaxy Buds are limited support.** They are recognized by name and shown as
  `제한적 지원`, but battery is **not** available: Galaxy Buds report battery over a
  proprietary, encrypted RFCOMM protocol, not in BLE advertisements or standard GATT. They
  are still captured in diagnostics bundles.
- **Generic GATT devices** only show a whole-device battery if they expose the standard
  BLE Battery Service (`0x180F`). Many mice/keyboards/earbuds don't; those show a clean
  "no battery" state rather than a fabricated value.
- **Battery percentages are the device's own coarse values** (often 10% steps for AirPods)
  and never interpolated. A missing part is simply absent, never shown as 0%.

## No-network guarantee

BudsMonitor performs **no network I/O** in normal operation. There is no account, no
analytics, and no telemetry. The codebase references no HTTP/socket APIs, and
`analyticsEnabled` / `networkEnabled` are forced off on every settings load/save. The only
runtime dependency that touches I/O is local file logging (Serilog file sink).

## Full reset

Quit BudsMonitor (tray → 종료), then delete both folders:

```
%AppData%\BudsMonitor
%LocalAppData%\BudsMonitor
```

They are recreated with defaults on next launch.
