# BudsMonitor for Windows

BudsMonitor is a local-only Windows tray utility for monitoring Bluetooth earbuds battery and status.

The repository has moved from the old LibrePods Windows feasibility fork into a new product direction:

- product name: `BudsMonitor for Windows`
- repository identity: `jeiel85/budsmonitor-windows`
- implementation stack: C# / .NET 10 / WPF
- primary UX: tray-first Windows desktop app
- privacy model: local-only, no account, no analytics, no telemetry, no normal-operation network calls

## Current Status

This repo is at the foundation stage.

Implemented now:

- `.NET 10.0.301` pinned by [`global.json`](./global.json)
- classic solution file at [`src/BudsMonitor.sln`](./src/BudsMonitor.sln)
- WPF app shell project at `src/BudsMonitor.App`
- domain/application/bluetooth/provider/infrastructure/diagnostics/test project skeletons
- minimal smoke test in `src/BudsMonitor.Tests`
- feasibility notes preserved under [`docs/research`](./docs/research/README.md)
- integrated production design bundle under [`docs/budsmonitor-integrated-design`](./docs/budsmonitor-integrated-design/README.md)

Not implemented yet:

- tray icon behavior
- AirPods parser port
- BLE advertisement scanner
- battery cache/settings/logging
- real device card UI
- notifications
- diagnostics export
- Generic GATT and Galaxy Buds providers

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
Tests: 1 passed
```

## Implementation Roadmap

The detailed sequence lives in [`docs/budsmonitor-integrated-design/docs/13-implementation-goals.md`](./docs/budsmonitor-integrated-design/docs/13-implementation-goals.md).

Near-term order:

1. GOAL 0: repository foundation and empty solution skeleton
2. GOAL 1: WPF tray shell
3. GOAL 2: settings, logging, cache foundation
4. GOAL 3: AirPods advertisement parser port
5. GOAL 4: BLE advertisement scanner
6. GOAL 5: AirPods provider integration

GOAL 0 is the current working branch state. GOAL 1 is the next implementation target.

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
