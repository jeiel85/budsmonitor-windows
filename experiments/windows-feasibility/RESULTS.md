# Windows Feasibility Probe Results

Date: 2026-05-09

Environment:

- OS: Windows 10.0.26200
- .NET SDK: 9.0.312
- CMake: 4.3.2 from Kitware, and Visual Studio bundled CMake 3.31.6-msvc6
- MSVC: 19.44.35226
- Qt: 6.8.3 `msvc2022_64` installed at `C:\Qt\6.8.3\msvc2022_64`
- Repository path: `D:\Project\librepods`

## Build

Command:

```powershell
dotnet build .\experiments\windows-feasibility\LibrePods.WindowsFeasibility.sln
```

Result:

- Success
- Warnings: 0
- Errors: 0

The solution now includes:

- `ble-advertisement-probe`
- `bluetooth-service-probe`
- `battery-tray-mvp`

## BLE advertisement probe

Command:

```powershell
dotnet run --project .\experiments\windows-feasibility\ble-advertisement-probe\BleAdvertisementProbe.csproj -- --seconds 12
```

Result:

- Apple manufacturer packets seen: 68
- Parsed AirPods proximity packets: 10
- Parsed candidate:
  - Model: AirPods 2, model id `0x0F20`
  - State: Playing Music
  - Battery: left unavailable, right 90%, case unavailable
  - In-ear: left false, right true

Interpretation:

- Windows can receive Apple manufacturer data through `BluetoothLEAdvertisementWatcher`.
- The current LibrePods BLE proximity parser can be ported to a Windows backend.
- Battery/status MVP is feasible.

## Bluetooth service probe

Command:

```powershell
dotnet run --project .\experiments\windows-feasibility\bluetooth-service-probe\BluetoothServiceProbe.csproj
```

Result:

- Known Classic Bluetooth devices included paired AirPods.
- Known BLE devices did not include the AirPods entry in this run.
- RFCOMM services matching `74ec2172-0bad-4d01-8f77-997b2be0722a`: none
- GATT services matching `74ec2172-0bad-4d01-8f77-997b2be0722a`: none

Interpretation:

- Windows high-level RFCOMM/GATT APIs do not expose the LibrePods AACP UUID on this machine.
- This does not prove AACP control is impossible.
- Next control-channel step: native Bluetooth SDP and socket probe, likely using Win32 Bluetooth APIs rather than WinRT selectors.

## Next recommended work

Completed after the first probe run:

- Extracted the Linux BLE advertisement parser into `linux/ble/bleadvertisementparser.*`.
- Moved shared BLE state into `linux/ble/bleinfo.h`.
- Reduced `linux/ble/blemanager.cpp` to scan orchestration plus parser invocation.
- Added `battery-tray-mvp`, a tiny Windows tray app that uses the confirmed BLE advertisement path.
- Added Windows-only CMake target `librepods-ble-parser-smoke`.
- Verified the smoke target configures, builds, and runs with MSVC and Qt 6.8.3.
- Added Windows C++/Qt/WinRT scanner wrapper `WindowsBleScanner`.
- Added Windows scanner smoke target `librepods-windows-ble-scanner-smoke`.
- Verified the C++/WinRT scanner target builds and receives AirPods BLE advertisements.
- Added `IBleScanner` and made both Linux `BleManager` and Windows `WindowsBleScanner` implement it.
- Added Windows Qt tray MVP target `librepods-windows-tray-mvp`.
- Verified the tray MVP builds and launches without immediate exit.
- Moved Windows scanner backend to `linux/platform/windows`.
- Queued WinRT advertisement callbacks back onto the Qt object thread before emitting scanner signals.

Windows scanner smoke result:

```powershell
$env:Path = "C:\Qt\6.8.3\msvc2022_64\bin;$env:Path"
.\build\windows-qt\librepods-windows-ble-scanner-smoke.exe
```

Observed result:

- Parsed AirPods BLE packets: 12 in 15 seconds
- Example parsed line: `AirPods BLE packet 70:8A:D7:22:E4:5D model=2 left=-1 right=0 case=-1`
- After adding `IBleScanner`, parsed AirPods BLE packets: 16 in 15 seconds
- After moving to `platform/windows` and queued signal delivery, parsed AirPods BLE packets: 11 in 15 seconds

Next recommended work:

1. Run the Windows Qt tray MVP interactively and verify tooltip/context-menu updates while opening/closing the AirPods case.
2. Move the tray MVP entrypoint itself from `tests` toward a production Windows app entrypoint.
3. Start a separate native AACP probe for SDP enumeration and possible L2CAP socket access.
