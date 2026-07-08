# Source Evidence Used for This Design

This bundle was created after reviewing `jeiel85/librepods-windows-ble` through the GitHub connector.

## Key evidence

1. The repository is a LibrePods Windows BLE-only fork, not a full Windows AACP control port. The root README says active control was abandoned because Windows blocks userspace L2CAP socket access, and the shipped scope is BLE-only: battery, in-ear detection, and proximity.
   - Source: `README.md`, lines 3–7 and 11–15 from reviewed output.

2. The root README lists confirmed outcomes: Windows BLE proximity packet reception works, tray battery display works, in-ear detection works, while L2CAP-based active control is blocked.
   - Source: `README.md`, lines 20–28.

3. The feasibility results show Apple manufacturer packets and parsed AirPods proximity packets were seen through `BluetoothLEAdvertisementWatcher`.
   - Source: `experiments/windows-feasibility/RESULTS.md`, lines 36–59.

4. The feasibility results show native SDP can discover AACP and PSM `0x1001`, but userspace L2CAP bind/connect fails with `WSAENETDOWN`.
   - Source: `experiments/windows-feasibility/RESULTS.md`, lines 131–166.

5. The final feasibility conclusion says Windows userspace AACP control is closed via standard paths, leaving WSL2 USB dongle passthrough, WDF driver, or BLE-only shipping as options.
   - Source: `experiments/windows-feasibility/RESULTS.md`, final conclusion lines 39–55 in the reviewed continuation.

6. `linux/platform/windows/README.md` states the Windows backend supports AirPods proximity BLE advertisements, left/right/case battery, charging state, in-ear state, tray tooltip/popover, and 30-second stale disconnect handling.
   - Source: `linux/platform/windows/README.md`, lines 17–23.

7. `windowsblescanner.cpp` uses Apple Company ID `0x004C`, `BluetoothLEAdvertisementWatcher`, and `BleAdvertisementParser::parseAppleManufacturerData(...)`.
   - Source: `linux/platform/windows/windowsblescanner.cpp`, lines 21–24 and 111–129.

8. `bleinfo.h` already models left/right/case battery, charging state, model, raw data, encrypted payload, in-ear state, case state, connection state, and last seen.
   - Source: `linux/ble/bleinfo.h`, lines 13–61.

9. The previous C# feasibility MVP includes an `AirPodsAdvertisementParser` with model mapping and battery nibble parsing.
   - Source: `experiments/windows-feasibility/battery-tray-mvp/Program.cs`, lines 159–245.

10. The repository license is GPLv3-or-later according to README and includes GPLv3 license text.
   - Source: `README.md`, lines 145–164; `LICENSE`, lines 24–40.
