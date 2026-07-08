# 03. Architecture

## Recommended stack

| Layer | Choice |
|---|---|
| Language | C# |
| Runtime | .NET 10 or current LTS available at implementation time |
| UI | WPF |
| Tray | `System.Windows.Forms.NotifyIcon` hosted by WPF app |
| Bluetooth advertisement | `Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher` |
| BLE GATT | `Windows.Devices.Bluetooth` + `Windows.Devices.Bluetooth.GenericAttributeProfile` |
| Logging | Serilog |
| Storage | JSON files under `%AppData%` / `%LocalAppData%` |
| Tests | xUnit |
| Packaging | Portable ZIP first, MSIX or installer later |

## Solution structure

```text
budsmonitor-windows/
  src/
    BudsMonitor.App/
    BudsMonitor.Domain/
    BudsMonitor.Application/
    BudsMonitor.Bluetooth/
    BudsMonitor.Providers.AirPods/
    BudsMonitor.Providers.Gatt/
    BudsMonitor.Providers.GalaxyBuds/
    BudsMonitor.Infrastructure/
    BudsMonitor.Diagnostics/
    BudsMonitor.Tests/
  docs/
  tools/
    legacy-probes/
  build/
  profiles/
```

## Runtime architecture

```text
┌─────────────────────────────────────────┐
│ BudsMonitor.App                         │
│ WPF shell, tray icon, windows, commands │
└─────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│ BudsMonitor.Application                 │
│ coordinators, refresh scheduler, rules  │
└─────────────────────────────────────────┘
                 │
                 ▼
┌─────────────────────────────────────────┐
│ Provider Resolution Layer               │
│ AirPods → Galaxy Buds → GATT → Cache    │
└─────────────────────────────────────────┘
       │                  │
       ▼                  ▼
┌───────────────┐  ┌─────────────────────┐
│ Bluetooth BLE │  │ Storage/Diagnostics │
│ scanner/GATT  │  │ cache/log/settings  │
└───────────────┘  └─────────────────────┘
```

## Core services

| Service | Responsibility |
|---|---|
| `DeviceRegistryService` | Tracks known, hidden, pinned, aliased devices |
| `DeviceExperienceResolver` | Maps device/provider data into user-facing card state |
| `BatteryRefreshCoordinator` | Coordinates provider reads and scanner updates |
| `BleAdvertisementScannerService` | Receives BLE advertisements and publishes events |
| `StandardGattClient` | Reads Battery Service characteristic values |
| `ProviderResolver` | Chooses best provider per device/event |
| `BatteryCacheRepository` | Persists last known snapshots |
| `NotificationRuleService` | Decides when to notify |
| `DiagnosticsExportService` | Writes diagnostic ZIP |
| `StartupRegistrationService` | Registers app for Windows startup |
| `SleepResumeService` | Handles suspend/resume recovery |
| `SingleInstanceService` | Prevents duplicate tray instances |

## Event flow: AirPods live advertisement

```text
BluetoothLEAdvertisementWatcher.Received
  → BleAdvertisementScannerService
  → AirPodsAdvertisementParser.TryParse
  → AirPodsBleAdvertisementProvider publishes snapshot
  → BatteryRefreshCoordinator updates BatteryCacheRepository
  → DeviceExperienceResolver builds DeviceCardState
  → UI updates tray tooltip/menu/main window
  → NotificationRuleService evaluates thresholds
```

## Event flow: Generic GATT refresh

```text
Refresh trigger
  → DeviceRegistryService returns known BLE devices
  → StandardGattBatteryProvider probes Battery Service
  → Read Battery Level characteristic
  → BatterySnapshot created
  → Cache + UI + notifications updated
```

## Threading model

Rules:

1. Bluetooth callbacks must not directly mutate WPF-bound view models.
2. Scanner events go through an application event channel.
3. UI updates are dispatched through WPF dispatcher.
4. Provider reads are cancellable and timeout-limited.
5. Same-device concurrent provider reads are forbidden.

Recommended primitives:

```csharp
System.Threading.Channels.Channel<BluetoothEvent>
SemaphoreSlim perDeviceLock
CancellationTokenSource for scanner lifetime
Dispatcher.InvokeAsync for UI projection
```

## Failure handling

Failures are first-class state, not exceptions hidden in logs.

```text
ProviderResult
  Success: BatterySnapshot
  Failure: reason + message + retry policy
```

Failures should be shown as:

- live value unavailable
- last known value available
- stale duration
- suggested action

## Project file sketch

```xml
<Project Sdk="Microsoft.NET.Sdk.WindowsDesktop">
  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0-windows10.0.17763.0</TargetFramework>
    <UseWPF>true</UseWPF>
    <UseWindowsForms>true</UseWindowsForms>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>
</Project>
```

If implementation begins before .NET 10 tooling is stable on the target machine, use `net8.0-windows10.0.17763.0` or the current installed LTS and document the choice.
