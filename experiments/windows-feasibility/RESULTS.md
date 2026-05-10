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

## Native AACP SDP/socket probe (2026-05-10)

Command:

```powershell
.\build\windows-qt\librepods-windows-aacp-probe.exe
```

Source: `linux/tests/windows_aacp_probe.cpp`  
CMake target: `librepods-windows-aacp-probe` (no Qt, links Bthprops + ws2_32)

### SDP query result

```
SDP record found!
Service name:  AAP Server
Remote BTH addr: C40B31C973D8  port/PSM: 4097  proto: 256
```

- `WSALookupServiceBeginW` with `NS_BTH` and AACP UUID finds the service on the paired AirPods.
- Service name is **"AAP Server"**, PSM is **0x1001 (4097)**, protocol is L2CAP (BTHPROTO_L2CAP = 256).
- This confirms the AACP control service is present and discoverable on Windows via native SDP.

### L2CAP socket result

| Attempt | Result | Error |
|---------|--------|-------|
| `socket(AF_BTH, SOCK_STREAM, BTHPROTO_L2CAP)` | socket() OK | — |
| `connect(…, PSM=0)` | **FAILED** | 10050 WSAENETDOWN |
| `connect(…, PSM=0x1001)` | **FAILED** | 10050 WSAENETDOWN |
| `socket(AF_BTH, SOCK_SEQPACKET, BTHPROTO_L2CAP)` | **FAILED** | 10044 WSAESOCKTNOSUPPORT |
| Local `bind(…, BT_PORT_ANY)` with SOCK_STREAM L2CAP | **FAILED** | 10050 WSAENETDOWN |

The local bind failure is the decisive result: even without a remote device involved, Windows returns
`WSAENETDOWN` on L2CAP bind. This is not a remote-device-not-connected issue — it is Windows
intentionally blocking userspace raw L2CAP access on the Classic Bluetooth adapter.

### Conclusion

| Layer | Result |
|-------|--------|
| BLE advertisement (proximity) | ✅ Works via `BluetoothLEAdvertisementWatcher` |
| SDP discovery (AACP UUID) | ✅ Works via `WSALookupServiceBeginW/NS_BTH` |
| L2CAP socket (userspace) | ❌ Blocked by Windows (WSAENETDOWN on local bind) |
| RFCOMM socket (userspace) | ❌ Wrong protocol; times out |

Windows does not expose raw Classic Bluetooth L2CAP to userspace applications.

### Next steps for control channel

Options (in order of invasiveness):

1. **Kernel driver (WDF/BthPort filter)** — full access but complex; requires code signing.
2. **Attach to the existing audio HFP/A2DP session** — AirPods maintain a BR/EDR connection when audio is active; investigate whether a registered Bluetooth profile can piggyback.
3. **Investigate `IOCTL_BTH_*` via `CreateFile` on the radio handle** — undocumented but some community projects use HCI IOCTLs directly.
4. **Adopt a helper process strategy** — run a small Linux VM or WSL2 BlueZ bridge via USB Bluetooth dongle passthrough (Hyper-V USB passthrough).

The most pragmatic near-term path without kernel development: **option 3 (HCI IOCTL investigation)** in a new probe.

## HCI IOCTL probe (2026-05-10)

Command:

```powershell
.\build\windows-qt\librepods-windows-hci-ioctl-probe.exe
```

Source: `linux/tests/windows_hci_ioctl_probe.cpp`  
CMake target: `librepods-windows-hci-ioctl-probe` (no Qt, links Bthprops)  
Header: `shared/bthioctl.h` (in Windows 10.0.26100.0 SDK)

### Radio-level IOCTL results

| IOCTL | Result | Notes |
|-------|--------|-------|
| `IOCTL_BTH_GET_LOCAL_INFO` | ✅ OK | Intel adapter (mfg=0x0002), HCI 0x0B (BT 5.2), LMP 0x0B |
| `IOCTL_BTH_GET_HOST_SUPPORTED_FEATURES` | ✅ OK | Enhanced Retransmission, Streaming, LE, SCO HCI bypass |
| `IOCTL_BTH_GET_DEVICE_INFO` | ✅ OK | 6 cached devices; AirPods found (see flags below) |
| `IOCTL_BTH_HCI_VENDOR_COMMAND` | ❌ 1314 | `ERROR_PRIVILEGE_NOT_HELD` — re-run as Administrator |

### AirPods device cache entry flags

`flags=0x0400539F` decodes to:  
`ADDRESS|COD|NAME|PAIRED|PERSONAL|VISIBLE|SSP_SUPPORTED|SSP_PAIRED|RSSI|LE_CONNECTED`

Key observation: **`LE_CONNECTED` is set but `CONNECTED` (=0x00000020, Classic BT) is NOT set**.  
The AirPods are connected via BLE but have no active BR/EDR (Classic Bluetooth) link.  
This is why `IOCTL_BTH_GET_RADIO_INFO` returns `ERROR_DEVICE_NOT_CONNECTED` (1167).  
BR/EDR activates when audio is playing (A2DP/HFP profile). Retry with active audio to test.

### SDP via IOCTL results

| IOCTL | Result | Notes |
|-------|--------|-------|
| `IOCTL_BTH_SDP_CONNECT` | ✅ OK | SDP handle obtained (via cached SDP data) |
| `IOCTL_BTH_SDP_SERVICE_SEARCH` | ✅ OK | 1 record handle returned: 0x4F498A30 |
| `IOCTL_BTH_SDP_ATTRIBUTE_SEARCH` | ✅ OK | 149 bytes — full SDP record |
| `IOCTL_BTH_SDP_SERVICE_ATTRIBUTE_SEARCH` | ✅ OK | 151 bytes — confirmed same |

### SDP record decoded

Raw bytes:
```
35 93 09 00 00 0A 4F 49 8A 30  09 00 01 35 11 1C
74 EC 21 72 0B AD 4D 01 8F 77  99 7B 2B E0 72 2A
09 00 02 0A 00 00 00 00  09 00 04 35 08 35 06
19 01 00 09 10 01  09 00 05 35 03 19 10 02 …
```

Decoded:
| Attribute | Value |
|-----------|-------|
| 0x0000 ServiceRecordHandle | `0x4F498A30` |
| 0x0001 ServiceClassIDList | `[{74ec2172-0bad-4d01-8f77-997b2be0722a}]` (AACP UUID) |
| 0x0002 ServiceRecordState | `0x00000000` |
| **0x0004 ProtocolDescriptorList** | **`[L2CAP, PSM=0x1001]`** ← confirmed again |
| 0x0005 BrowseGroupList | `[PublicBrowseRoot]` |

### Conclusion

Public `IOCTL_BTH_*` from userspace allow:
- Full radio info and host feature query ✅
- Cached device list with connection flags ✅
- Full SDP record retrieval (service search + attribute search) ✅

Blocked from userspace:
- `IOCTL_BTH_HCI_VENDOR_COMMAND` — needs admin elevation (ERROR_PRIVILEGE_NOT_HELD)
- L2CAP socket connect/bind — blocked by Winsock BT stack (WSAENETDOWN)
- `IOCTL_INTERNAL_BTH_SUBMIT_BRB` — kernel-internal only (METHOD_NEITHER)

### Next steps

1. **Run probe as Administrator** — test if `IOCTL_BTH_HCI_VENDOR_COMMAND` opens. If yes, raw HCI commands are possible from admin-elevated userspace.
2. **Trigger BR/EDR connection (play audio) then re-run** — `IOCTL_BTH_GET_RADIO_INFO` and L2CAP socket may behave differently when a Classic BT ACL link is active.
3. **Kernel driver (WDF BthPort filter)** — required for `IOCTL_INTERNAL_BTH_SUBMIT_BRB` / arbitrary L2CAP. No code signing needed for development with test-signing mode.
