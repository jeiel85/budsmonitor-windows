# Windows Feasibility Probes

These probes test the two Windows capabilities that decide how much of LibrePods can be ported.

Build both probes:

```powershell
dotnet build .\LibrePods.WindowsFeasibility.sln
```

## Native CMake/Qt smoke target

The repo now has a Windows-only CMake smoke target that verifies the extracted C++ BLE parser with MSVC and Qt:

```powershell
cmd /c "call C:\BuildTools\Common7\Tools\VsDevCmd.bat -arch=x64 && cmake -S ..\..\linux -B ..\..\build\windows-qt -G Ninja -DCMAKE_PREFIX_PATH=C:\Qt\6.8.3\msvc2022_64 && cmake --build ..\..\build\windows-qt"
$env:Path = "C:\Qt\6.8.3\msvc2022_64\bin;$env:Path"
..\..\build\windows-qt\librepods-ble-parser-smoke.exe
```

This does not build the full app yet. It is a bridge step that confirms the common parser code can compile on Windows before the Windows backend is added.

## Probe 1: BLE advertisement

This checks whether Windows can see Apple manufacturer data from AirPods BLE proximity advertisements. If this works, a Windows battery/status app is feasible.

```powershell
dotnet run --project .\ble-advertisement-probe -- --seconds 45
```

Expected useful output:

- Apple manufacturer data with company id `0x004C`
- AirPods proximity payload starting with `0x07`
- Parsed model, battery values, charging flags, lid state, in-ear flags

Tips:

- Open the AirPods case near the PC.
- Make sure Windows Bluetooth is enabled.
- If nothing appears, try unpairing/re-pairing or toggling Bluetooth.

## Probe 2: Bluetooth service discovery

This checks whether Windows exposes the AirPods AACP UUID through common Windows APIs. If this exposes a usable service, full control features become more realistic. If it finds nothing, AACP may still exist but not through normal RFCOMM/GATT APIs.

```powershell
dotnet run --project .\bluetooth-service-probe
```

The UUID under test is:

```text
74ec2172-0bad-4d01-8f77-997b2be0722a
```

Interpretation:

- BLE advertisement probe succeeds, service probe fails: battery/status MVP is still feasible; control channel needs deeper native socket research.
- Both probes succeed: proceed to AACP handshake PoC.
- Both fail: investigate adapter, Windows permissions, and AirPods pairing state before refactoring the main app.

## Probe 3: Battery tray MVP

This is a tiny Windows tray app that uses the successful BLE advertisement path and shows the latest parsed AirPods battery/status in the tray tooltip and context menu.

```powershell
dotnet run --project .\battery-tray-mvp
```

It is intentionally experimental and duplicates the C# parser from the BLE probe. The production path should use the C++ parser extracted into `linux/ble/bleadvertisementparser.*` or a future shared core module.

## Current local result

On 2026-05-09, both probes built successfully on Windows 10.0.26200 with .NET SDK 9.0.312.

The BLE advertisement probe successfully received and parsed AirPods proximity packets. This confirms that a Windows battery/status backend is feasible on this machine.

The service probe did not find the AACP UUID through RFCOMM or GATT selectors. This means control features need a lower-level native SDP/socket PoC before they can be considered feasible.
