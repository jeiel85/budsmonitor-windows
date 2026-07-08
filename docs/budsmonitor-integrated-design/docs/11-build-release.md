# 11. Build and Release Plan

## Build strategy

Start with a portable ZIP release. Add installer/MSIX only after daily-driver stability is proven.

## Development prerequisites

- Windows 10 1809+ or Windows 11
- Visual Studio 2022 or Build Tools
- .NET SDK LTS selected for implementation
- Windows SDK

## Repository structure

```text
.github/workflows/build-windows.yml
build/package.ps1
src/BudsMonitor.App/BudsMonitor.App.csproj
src/BudsMonitor.sln
```

## Build commands

```powershell
dotnet restore .\src\BudsMonitor.sln
dotnet build .\src\BudsMonitor.sln -c Release
dotnet test .\src\BudsMonitor.sln -c Release
dotnet publish .\src\BudsMonitor.App\BudsMonitor.App.csproj -c Release -r win-x64 --self-contained false
```

## Portable package layout

```text
BudsMonitor-win-x64-v1.0.0/
  BudsMonitor.exe
  BudsMonitor.*.dll
  profiles/
  docs/
    PRIVACY.md
    TROUBLESHOOTING.md
  tools/
    diagnostics-readme.md
```

## CI workflow outline

```yaml
name: Build BudsMonitor Windows

on:
  push:
    branches: [ main ]
  pull_request:
  workflow_dispatch:

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet restore src/BudsMonitor.sln
      - run: dotnet build src/BudsMonitor.sln -c Release --no-restore
      - run: dotnet test src/BudsMonitor.sln -c Release --no-build
      - run: dotnet publish src/BudsMonitor.App/BudsMonitor.App.csproj -c Release -r win-x64 --self-contained false -o artifacts/BudsMonitor
      - uses: actions/upload-artifact@v4
        with:
          name: BudsMonitor-win-x64
          path: artifacts/BudsMonitor
```

If .NET 10 SDK is unavailable in CI at implementation time, pin to the current stable LTS and update this file.

## Versioning

Use semantic versioning:

```text
v0.1.0 internal shell
v0.2.0 AirPods provider usable
v0.3.0 cache/notifications/diagnostics
v0.4.0 generic GATT provider
v0.5.0 daily-driver beta
v1.0.0 first stable daily-driver release
```

## Release criteria

A release artifact may be created only when:

- build passes
- tests pass
- no known crash on startup
- no network references found
- privacy docs included
- diagnostics generation tested
- app runs from extracted ZIP

## Startup registration

Implement startup registration without admin rights.

Preferred options:

1. Registry current user Run key
2. Startup folder shortcut
3. MSIX startup task if using MSIX later

Do not require admin installation for normal use.

## Signing

Initial personal build can be unsigned.

For broader release:

- Sign executable if distributing publicly.
- Avoid kernel driver path unless project scope changes drastically.

## Release notes template

```markdown
# BudsMonitor vX.Y.Z

## Highlights
- ...

## Supported devices
- AirPods BLE: ...
- Generic BLE Battery Service: ...
- Galaxy Buds: ...

## Known limitations
- AirPods active controls are not supported on Windows userspace.
- Some advertisement-derived battery values may update in coarse increments.

## Privacy
- No analytics, no ads, no account, no network calls.
```
