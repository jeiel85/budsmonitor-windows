# BudsMonitor for Windows

BudsMonitor is a local-only Windows tray utility for monitoring Bluetooth earbuds battery and status.

The repository has moved from the old LibrePods Windows feasibility fork into a new product direction:

- product name: `BudsMonitor for Windows`
- repository identity: `jeiel85/budsmonitor-windows`
- implementation stack: C# / .NET 10 / WPF
- primary UX: tray-first Windows desktop app
- privacy model: local-only, no account, no analytics, no telemetry, no normal-operation network calls

## Current Status

Beta — GOAL 0 through GOAL 12 of the implementation roadmap are complete. BudsMonitor is a
daily-driver tray utility backed by 80 passing unit tests.

Implemented:

- `.NET 10.0.301` pinned by [`global.json`](./global.json); solution at [`src/BudsMonitor.sln`](./src/BudsMonitor.sln)
- tray-first WPF shell (single instance, close-to-tray, quit), Notion-style light/dark UI
- AirPods BLE proximity advertisement parser + scanner + provider (left/right/case, charging)
- last-known cache with stale-state display and freshness badges
- low-battery notifications with repeat suppression and quiet hours
- generic standard-GATT Battery Service provider for other BLE devices
- device management: pin / hide / alias, persisted per device
- local diagnostics ZIP export (Bluetooth addresses masked by default, nothing transmitted)
- sleep/resume and Bluetooth on/off self-recovery with failure backoff
- Galaxy Buds limited-support track (name detection only; no battery — proprietary protocol)
- settings, 14-day rolling file logs, cache, and device registry under the user profile

Known limitations are documented honestly in [`docs/TROUBLESHOOTING.md`](./docs/TROUBLESHOOTING.md).

## Product Scope

Planned v1.0 scope:

- AirPods BLE proximity advertisement monitoring
- left/right/case battery where advertisements provide it
- charging and in-ear status where advertisements provide it
- last-known cache and stale-state display
- low-battery notifications
- Generic BLE Battery Service fallback
- Galaxy Buds diagnostics track
- local settings, logs, cache, and diagnostics export

Explicit v1.0 non-goals:

- AirPods ANC / transparency control
- conversation awareness control
- head gesture configuration
- firmware management
- AirPods rename or deep configuration
- custom Windows kernel driver
- WSL2 USB Bluetooth dongle bridge as a normal-user dependency
- cloud sync, accounts, analytics, ads, or telemetry

The old feasibility work showed that normal Windows userspace can receive AirPods BLE proximity advertisements, but cannot open the AirPods active-control path through raw Classic Bluetooth L2CAP.

## Repository Layout

```text
src/
  BudsMonitor.sln
  BudsMonitor.App/                 WPF app shell
  BudsMonitor.Domain/              shared domain contracts and models
  BudsMonitor.Application/         coordinators and app services
  BudsMonitor.Bluetooth/           Windows Bluetooth integration
  BudsMonitor.Providers.AirPods/   AirPods BLE provider
  BudsMonitor.Providers.Gatt/      standard BLE Battery Service provider
  BudsMonitor.Providers.GalaxyBuds/Galaxy Buds provider track
  BudsMonitor.Infrastructure/      settings, cache, logging, persistence
  BudsMonitor.Diagnostics/         diagnostics export
  BudsMonitor.Tests/               xUnit tests

docs/
  budsmonitor-integrated-design/   production design bundle
  research/                        feasibility and migration notes

experiments/windows-feasibility/   legacy .NET feasibility probes
linux/                            legacy LibrePods/Qt/C++ reference code
```

## Build and Test

Requirements:

- Windows 10 1809+ or Windows 11
- .NET 10 SDK

This repository pins SDK `10.0.301` in [`global.json`](./global.json). If you installed .NET with the user-local installer, make sure `~/.dotnet` is on `PATH` for the shell you are using.

PowerShell:

```powershell
$env:Path = "$env:USERPROFILE\.dotnet;$env:Path"
dotnet restore .\src\BudsMonitor.sln
dotnet build .\src\BudsMonitor.sln -c Release --no-restore
dotnet test .\src\BudsMonitor.sln -c Release --no-build
```

Git Bash:

```bash
export PATH="$HOME/.dotnet:$PATH"
dotnet restore ./src/BudsMonitor.sln
dotnet build ./src/BudsMonitor.sln -c Release --no-restore
dotnet test ./src/BudsMonitor.sln -c Release --no-build
```

Current expected result:

```text
Build: warnings 0, errors 0
Tests: 80 passed
```

## Portable build

To produce a self-contained portable build (no .NET install required on the target machine):

```powershell
pwsh -File scripts\publish-portable.ps1
```

This publishes `src/BudsMonitor.App` self-contained for `win-x64` and writes both an
unzipped folder and `BudsMonitor-portable-win-x64.zip` under `dist/` (git-ignored). Unzip
anywhere on Windows 10 1809+/11 and run `BudsMonitor.App.exe`.

## Implementation Roadmap

The detailed sequence lives in [`docs/budsmonitor-integrated-design/docs/13-implementation-goals.md`](./docs/budsmonitor-integrated-design/docs/13-implementation-goals.md).

All 13 goals (GOAL 0 – GOAL 12) are implemented:

1. GOAL 0: repository foundation and solution skeleton
2. GOAL 1: WPF tray shell
3. GOAL 2: settings, logging, cache foundation
4. GOAL 3: AirPods advertisement parser port
5. GOAL 4: BLE advertisement scanner
6. GOAL 5: AirPods provider integration
7. GOAL 6: low-battery notification engine
8. GOAL 7: generic standard-GATT Battery Service provider
9. GOAL 8: device management (pin / hide / alias)
10. GOAL 9: local diagnostics export
11. GOAL 10: sleep/resume and self-repair
12. GOAL 11: Galaxy Buds limited-support track
13. GOAL 12: daily-driver beta hardening (this milestone)

## Legacy Research Source

This repository was previously `jeiel85/librepods-windows-ble`, a Windows BLE-only LibrePods fork. That work remains valuable as feasibility evidence and parser/scanner reference material, but production work now belongs in the BudsMonitor solution under `src/`.

Key references:

- [`docs/research/windows-aacp-feasibility.md`](./docs/research/windows-aacp-feasibility.md)
- [`docs/research/legacy-libre-pods-windows-porting.md`](./docs/research/legacy-libre-pods-windows-porting.md)
- [`experiments/windows-feasibility/RESULTS.md`](./experiments/windows-feasibility/RESULTS.md)
- [`docs/windows-porting-progress.md`](./docs/windows-porting-progress.md)
- [`docs/wsl2-usbipd-guide.md`](./docs/wsl2-usbipd-guide.md)

The legacy Qt/C++ Windows tray MVP and native probes are kept for reference. They are not the target architecture for the production BudsMonitor app.

## License

The repository inherits the original LibrePods license terms: GNU GPL v3.0 or later. See [`LICENSE`](./LICENSE).

If code is copied or adapted from the legacy LibrePods fork, BudsMonitor should remain GPLv3-or-later for distribution. If a future closed-source product is desired, re-derive the implementation independently from public protocol facts and fresh testing rather than copying GPL code.
