// windows_hci_ioctl_probe.cpp
//
// Probes Windows Bluetooth IOCTL interface via DeviceIoControl on the radio
// handle returned by BluetoothFindFirstRadio.
//
// Goal: determine which IOCTL_BTH_* calls are accessible from userspace and
// whether they open any path toward an L2CAP connection with AirPods.
//
// Build: librepods-windows-hci-ioctl-probe (no Qt)
//
// APIs used:
//   BluetoothFindFirstRadio / BluetoothFindNextRadio  (Bthprops.lib)
//   DeviceIoControl                                    (kernel32.lib)
//   IOCTLs from bthioctl.h: GET_LOCAL_INFO, GET_RADIO_INFO,
//   GET_DEVICE_INFO, SDP_CONNECT, SDP_SERVICE_ATTRIBUTE_SEARCH,
//   GET_HOST_SUPPORTED_FEATURES, HCI_VENDOR_COMMAND

#define WIN32_LEAN_AND_MEAN
#define NTDDI_VERSION NTDDI_WIN8   // enable Win8+ IOCTLs
#define _WIN32_WINNT  0x0602
#ifndef UNICODE
#define UNICODE
#endif

#include <windows.h>
#include <winioctl.h>
#include <bluetoothapis.h>
#include <bthdef.h>
#include <bthsdpdef.h>
#include <bthioctl.h>
#include <ws2bth.h>          // BTH_ADDR, BTH_DEVICE_INFO

#include <cstdio>
#include <vector>
#include <string>

// AACP UUID {74ec2172-0bad-4d01-8f77-997b2be0722a}
static const GUID AACP_UUID = {
    0x74ec2172, 0x0bad, 0x4d01,
    {0x8f, 0x77, 0x99, 0x7b, 0x2b, 0xe0, 0x72, 0x2a}
};

// -------------------------------------------------------------------------
// Helper: convert BTH_ADDR to human-readable string
// -------------------------------------------------------------------------
static std::wstring bthAddrStr(BTH_ADDR addr)
{
    wchar_t buf[32];
    swprintf_s(buf, L"%02llX:%02llX:%02llX:%02llX:%02llX:%02llX",
               (addr >> 40) & 0xFF, (addr >> 32) & 0xFF,
               (addr >> 24) & 0xFF, (addr >> 16) & 0xFF,
               (addr >> 8)  & 0xFF, (addr)       & 0xFF);
    return buf;
}

static BTH_ADDR bluetoothAddressToBthAddr(BLUETOOTH_ADDRESS addr)
{
    BTH_ADDR result = 0;
    for (int i = 0; i < 6; ++i)
        result |= (BTH_ADDR)addr.rgBytes[i] << (i * 8);
    return result;
}

// -------------------------------------------------------------------------
// 1. IOCTL_BTH_GET_LOCAL_INFO
// -------------------------------------------------------------------------
static void probeLocalInfo(HANDLE hRadio)
{
    wprintf(L"\n--- IOCTL_BTH_GET_LOCAL_INFO ---\n");

    BTH_LOCAL_RADIO_INFO info = {};
    DWORD returned = 0;
    BOOL ok = DeviceIoControl(hRadio, IOCTL_BTH_GET_LOCAL_INFO,
                              nullptr, 0,
                              &info, sizeof(info),
                              &returned, nullptr);
    if (!ok) {
        wprintf(L"  FAILED: %lu\n", GetLastError());
        return;
    }

    wprintf(L"  addr:          %s\n", bthAddrStr(info.localInfo.address).c_str());
    wprintf(L"  name:          %S\n", info.localInfo.name);      // name is char[]
    wprintf(L"  classOfDevice: 0x%06lX\n", info.localInfo.classOfDevice);
    wprintf(L"  flags:         0x%08lX\n", info.flags);
    wprintf(L"  hciRevision:   0x%04X\n",  info.hciRevision);
    wprintf(L"  hciVersion:    0x%02X\n",  info.hciVersion);
    wprintf(L"  lmpVersion:    0x%02X\n",  info.radioInfo.lmpVersion);
    wprintf(L"  lmpSubversion: 0x%04X\n",  info.radioInfo.lmpSubversion);
    wprintf(L"  manufacturer:  0x%04X\n",  info.radioInfo.mfg);
    wprintf(L"  lmpFeatures:   0x%016llX\n", info.radioInfo.lmpSupportedFeatures);
}

// -------------------------------------------------------------------------
// 2. IOCTL_BTH_GET_RADIO_INFO (for a remote device)
// -------------------------------------------------------------------------
static void probeRemoteRadioInfo(HANDLE hRadio, BTH_ADDR remoteAddr,
                                  const wchar_t *devName)
{
    wprintf(L"\n--- IOCTL_BTH_GET_RADIO_INFO for %s ---\n", devName);

    BTH_RADIO_INFO info = {};
    DWORD returned = 0;
    BOOL ok = DeviceIoControl(hRadio, IOCTL_BTH_GET_RADIO_INFO,
                              &remoteAddr, sizeof(remoteAddr),
                              &info, sizeof(info),
                              &returned, nullptr);
    if (!ok) {
        wprintf(L"  FAILED: %lu\n", GetLastError());
        return;
    }
    wprintf(L"  lmpVersion:    0x%02X\n",  info.lmpVersion);
    wprintf(L"  lmpSubversion: 0x%04X\n",  info.lmpSubversion);
    wprintf(L"  manufacturer:  0x%04X\n",  info.mfg);
    wprintf(L"  lmpFeatures:   0x%016llX\n", info.lmpSupportedFeatures);
}

// -------------------------------------------------------------------------
// 3. IOCTL_BTH_GET_DEVICE_INFO (cached discovered device list)
// -------------------------------------------------------------------------
static void printBdifFlags(ULONG flags)
{
    // BDIF_* flags from bthdef.h
    struct { ULONG bit; const wchar_t *name; } kFlags[] = {
        {0x00000001, L"ADDRESS"},    {0x00000002, L"COD"},
        {0x00000004, L"NAME"},       {0x00000008, L"PAIRED"},
        {0x00000010, L"PERSONAL"},   {0x00000020, L"CONNECTED"},
        {0x00000040, L"SHORT_NAME"}, {0x00000080, L"VISIBLE"},
        {0x00000100, L"SSP_SUPPORTED"}, {0x00000200, L"SSP_PAIRED"},
        {0x00000400, L"SSP_MITM_PROTECTED"},
        {0x00001000, L"RSSI"},       {0x00002000, L"EIR"},
        {0x00200000, L"LE_PAIRED"},  {0x00400000, L"LE_PERSONAL"},
        {0x04000000, L"LE_CONNECTED"},{0x08000000, L"LE_DISCOVERABLE"},
    };
    bool first = true;
    for (auto &f : kFlags) {
        if (flags & f.bit) {
            if (!first) wprintf(L"|");
            wprintf(L"%s", f.name);
            first = false;
        }
    }
}

static void probeDeviceInfo(HANDLE hRadio)
{
    wprintf(L"\n--- IOCTL_BTH_GET_DEVICE_INFO ---\n");

    const ULONG maxDevices = 32;
    const DWORD bufSize = sizeof(BTH_DEVICE_INFO_LIST)
                        + (maxDevices - 1) * sizeof(BTH_DEVICE_INFO);
    std::vector<BYTE> buf(bufSize, 0);
    auto *list = reinterpret_cast<BTH_DEVICE_INFO_LIST *>(buf.data());
    list->numOfDevices = maxDevices;

    DWORD returned = 0;
    BOOL ok = DeviceIoControl(hRadio, IOCTL_BTH_GET_DEVICE_INFO,
                              nullptr, 0,
                              list, bufSize,
                              &returned, nullptr);
    if (!ok) {
        wprintf(L"  FAILED: %lu\n", GetLastError());
        return;
    }

    wprintf(L"  Devices in cache: %lu\n", list->numOfDevices);
    for (ULONG i = 0; i < list->numOfDevices; ++i) {
        const BTH_DEVICE_INFO &d = list->deviceList[i];
        wprintf(L"  [%lu] %-30S  addr=%s  cod=0x%06lX\n",
                i, d.name, bthAddrStr(d.address).c_str(), d.classOfDevice);
        wprintf(L"       flags=0x%08lX  (", d.flags);
        printBdifFlags(d.flags);
        wprintf(L")\n");
    }
}

// -------------------------------------------------------------------------
// 4. IOCTL_BTH_GET_HOST_SUPPORTED_FEATURES
// -------------------------------------------------------------------------
static void probeHostFeatures(HANDLE hRadio)
{
    wprintf(L"\n--- IOCTL_BTH_GET_HOST_SUPPORTED_FEATURES ---\n");

    BTH_HOST_FEATURE_MASK feat = {};
    DWORD returned = 0;
    BOOL ok = DeviceIoControl(hRadio, IOCTL_BTH_GET_HOST_SUPPORTED_FEATURES,
                              nullptr, 0,
                              &feat, sizeof(feat),
                              &returned, nullptr);
    if (!ok) {
        wprintf(L"  FAILED: %lu\n", GetLastError());
        return;
    }
    wprintf(L"  FeatureMask: 0x%016llX\n", feat.Mask);
    if (feat.Mask & BTH_HOST_FEATURE_ENHANCED_RETRANSMISSION_MODE)
        wprintf(L"    ENHANCED_RETRANSMISSION_MODE\n");
    if (feat.Mask & BTH_HOST_FEATURE_STREAMING_MODE)
        wprintf(L"    STREAMING_MODE\n");
    if (feat.Mask & BTH_HOST_FEATURE_LOW_ENERGY)
        wprintf(L"    LOW_ENERGY\n");
    if (feat.Mask & BTH_HOST_FEATURE_SCO_HCI)
        wprintf(L"    SCO_HCI\n");
    if (feat.Mask & BTH_HOST_FEATURE_SCO_HCIBYPASS)
        wprintf(L"    SCO_HCIBYPASS\n");
}

// -------------------------------------------------------------------------
// 5. IOCTL_BTH_SDP_CONNECT + IOCTL_BTH_SDP_SERVICE_ATTRIBUTE_SEARCH
// -------------------------------------------------------------------------
static void probeSdpIoctl(HANDLE hRadio, BTH_ADDR remoteAddr,
                           const wchar_t *devName)
{
    wprintf(L"\n--- IOCTL_BTH_SDP_CONNECT to %s ---\n", devName);

    BTH_SDP_CONNECT sdpConn = {};
    sdpConn.bthAddress = remoteAddr;
    sdpConn.fSdpConnect = SDP_CONNECT_CACHE;
    sdpConn.requestTimeout = SDP_REQUEST_TO_DEFAULT;

    DWORD returned = 0;
    BOOL ok = DeviceIoControl(hRadio, IOCTL_BTH_SDP_CONNECT,
                              &sdpConn, sizeof(sdpConn),
                              &sdpConn, sizeof(sdpConn),
                              &returned, nullptr);
    if (!ok) {
        wprintf(L"  SDP_CONNECT FAILED: %lu\n", GetLastError());
        return;
    }
    wprintf(L"  SDP_CONNECT OK — handle=0x%llX\n", sdpConn.hConnection);

    // --- SERVICE SEARCH: find AACP record handles ---
    wprintf(L"\n--- IOCTL_BTH_SDP_SERVICE_SEARCH ---\n");
    {
        BTH_SDP_SERVICE_SEARCH_REQUEST ssReq = {};
        ssReq.hConnection = sdpConn.hConnection;
        ssReq.uuids[0].uuidType = SDP_ST_UUID128;
        ssReq.uuids[0].u.uuid128 = AACP_UUID;

        const DWORD maxRecords = 8;
        std::vector<ULONG> handles(maxRecords, 0);
        DWORD ssReturned = 0;
        BOOL ssOk = DeviceIoControl(hRadio, IOCTL_BTH_SDP_SERVICE_SEARCH,
                                    &ssReq, sizeof(ssReq),
                                    handles.data(), maxRecords * sizeof(ULONG),
                                    &ssReturned, nullptr);
        if (!ssOk) {
            wprintf(L"  FAILED: %lu\n", GetLastError());
        } else {
            ULONG numHandles = ssReturned / sizeof(ULONG);
            wprintf(L"  Record handles returned: %lu\n", numHandles);
            for (ULONG i = 0; i < numHandles; ++i)
                wprintf(L"    [%lu] handle=0x%08lX\n", i, handles[i]);

            // --- ATTRIBUTE SEARCH on each returned record handle ---
            for (ULONG i = 0; i < numHandles; ++i) {
                wprintf(L"\n--- IOCTL_BTH_SDP_ATTRIBUTE_SEARCH (handle=0x%08lX) ---\n",
                        handles[i]);

                BTH_SDP_ATTRIBUTE_SEARCH_REQUEST attrReq = {};
                attrReq.hConnection = sdpConn.hConnection;
                attrReq.searchFlags = 0;
                attrReq.recordHandle = handles[i];
                attrReq.range[0].minAttribute = 0x0000;
                attrReq.range[0].maxAttribute = 0xFFFF;

                const DWORD respBufSize = 65536;
                std::vector<BYTE> respBuf(respBufSize, 0);
                auto *resp = reinterpret_cast<BTH_SDP_STREAM_RESPONSE *>(respBuf.data());

                ok = DeviceIoControl(hRadio, IOCTL_BTH_SDP_ATTRIBUTE_SEARCH,
                                     &attrReq, sizeof(attrReq),
                                     resp, respBufSize,
                                     &returned, nullptr);
                if (!ok) {
                    wprintf(L"  ATTRIBUTE_SEARCH FAILED: %lu\n", GetLastError());
                } else {
                    wprintf(L"  responseSize: %lu  requiredSize: %lu\n",
                            resp->responseSize, resp->requiredSize);
                    if (resp->responseSize > 0) {
                        wprintf(L"  Raw SDP bytes (first 64):");
                        ULONG printLen = min(resp->responseSize, 64UL);
                        for (ULONG j = 0; j < printLen; ++j) {
                            if (j % 16 == 0) wprintf(L"\n    ");
                            wprintf(L"%02X ", resp->response[j]);
                        }
                        wprintf(L"\n");
                    }
                }
            }
        }
    }

    // --- SERVICE ATTRIBUTE SEARCH (combined, one shot) ---
    wprintf(L"\n--- IOCTL_BTH_SDP_SERVICE_ATTRIBUTE_SEARCH ---\n");
    {
        // sizeof already includes range[1]; no extra padding needed
        const DWORD reqBufSize = sizeof(BTH_SDP_SERVICE_ATTRIBUTE_SEARCH_REQUEST);
        std::vector<BYTE> reqBuf(reqBufSize, 0);
        auto *req = reinterpret_cast<BTH_SDP_SERVICE_ATTRIBUTE_SEARCH_REQUEST *>(reqBuf.data());
        req->hConnection = sdpConn.hConnection;
        req->searchFlags = 0;
        req->uuids[0].uuidType = SDP_ST_UUID128;
        req->uuids[0].u.uuid128 = AACP_UUID;
        req->range[0].minAttribute = 0x0000;
        req->range[0].maxAttribute = 0xFFFF;

        const DWORD respBufSize = 65536;
        std::vector<BYTE> respBuf(respBufSize, 0);
        auto *resp = reinterpret_cast<BTH_SDP_STREAM_RESPONSE *>(respBuf.data());

        ok = DeviceIoControl(hRadio, IOCTL_BTH_SDP_SERVICE_ATTRIBUTE_SEARCH,
                             req, reqBufSize,
                             resp, respBufSize,
                             &returned, nullptr);
        if (!ok) {
            wprintf(L"  FAILED: %lu\n", GetLastError());
        } else {
            wprintf(L"  responseSize: %lu  requiredSize: %lu\n",
                    resp->responseSize, resp->requiredSize);
            if (resp->responseSize > 0) {
                wprintf(L"  Raw SDP bytes (first 64):");
                ULONG printLen = min(resp->responseSize, 64UL);
                for (ULONG j = 0; j < printLen; ++j) {
                    if (j % 16 == 0) wprintf(L"\n    ");
                    wprintf(L"%02X ", resp->response[j]);
                }
                wprintf(L"\n");
            }
        }
    }

    // Disconnect SDP
    DeviceIoControl(hRadio, IOCTL_BTH_SDP_DISCONNECT,
                    &sdpConn.hConnection, sizeof(sdpConn.hConnection),
                    nullptr, 0, &returned, nullptr);
    wprintf(L"  SDP session closed\n");
}

// -------------------------------------------------------------------------
// 6. IOCTL_BTH_HCI_VENDOR_COMMAND — Read Local Version Information
//    (HCI opcode 0x1001, OGF=0x04 OCF=0x001)
//    Tests whether the vendor-command path is open at all.
//    ManufacturerId=0 and LmpVersion=0 → send regardless.
// -------------------------------------------------------------------------
static void probeHciVendorCmd(HANDLE hRadio)
{
    wprintf(L"\n--- IOCTL_BTH_HCI_VENDOR_COMMAND (Read Local Version, opcode=0x1001) ---\n");

    // Build BTH_VENDOR_SPECIFIC_COMMAND with zero-length Data array
    const DWORD cmdSize = sizeof(BTH_VENDOR_SPECIFIC_COMMAND);
    std::vector<BYTE> cmdBuf(cmdSize, 0);
    auto *cmd = reinterpret_cast<BTH_VENDOR_SPECIFIC_COMMAND *>(cmdBuf.data());
    cmd->ManufacturerId = 0;          // match any manufacturer
    cmd->LmpVersion = 0;              // match any LMP version
    cmd->MatchAnySinglePattern = TRUE;
    cmd->HciHeader.OpCode = 0x1001;   // HCI Read Local Version Information
    cmd->HciHeader.TotalParameterLength = 0;

    const DWORD respBufSize = 256;
    std::vector<BYTE> respBuf(respBufSize, 0);
    DWORD returned = 0;

    BOOL ok = DeviceIoControl(hRadio, IOCTL_BTH_HCI_VENDOR_COMMAND,
                              cmd, cmdSize,
                              respBuf.data(), respBufSize,
                              &returned, nullptr);
    if (!ok) {
        DWORD err = GetLastError();
        wprintf(L"  FAILED: %lu", err);
        if (err == ERROR_INVALID_PARAMETER)
            wprintf(L" (INVALID_PARAMETER — may require matching patterns in Data[])");
        else if (err == ERROR_ACCESS_DENIED)
            wprintf(L" (ACCESS_DENIED — vendor commands are restricted)");
        else if (err == ERROR_PRIVILEGE_NOT_HELD)
            wprintf(L" (PRIVILEGE_NOT_HELD — re-run as Administrator to test)");
        wprintf(L"\n");
    } else {
        wprintf(L"  OK — returned %lu bytes:", returned);
        for (DWORD i = 0; i < returned; ++i) {
            if (i % 16 == 0) wprintf(L"\n    ");
            wprintf(L"%02X ", respBuf[i]);
        }
        wprintf(L"\n");
    }
}

// -------------------------------------------------------------------------
// main
// -------------------------------------------------------------------------
int main()
{
    wprintf(L"LibrePods Windows HCI IOCTL Probe\n\n");

    // Find first Bluetooth radio
    BLUETOOTH_FIND_RADIO_PARAMS rfParam = { sizeof(rfParam) };
    HANDLE hRadio = INVALID_HANDLE_VALUE;
    HBLUETOOTH_RADIO_FIND hFind = BluetoothFindFirstRadio(&rfParam, &hRadio);
    if (!hFind) {
        wprintf(L"BluetoothFindFirstRadio failed: %lu\n", GetLastError());
        return 1;
    }
    wprintf(L"Bluetooth radio handle: 0x%p\n", hRadio);

    // --- Radio-level IOCTLs ---
    probeLocalInfo(hRadio);
    probeHostFeatures(hRadio);
    probeDeviceInfo(hRadio);
    probeHciVendorCmd(hRadio);

    // --- Find paired AirPods for device-targeted IOCTLs ---
    BLUETOOTH_DEVICE_SEARCH_PARAMS params = {};
    params.dwSize = sizeof(params);
    params.fReturnAuthenticated = TRUE;
    params.fReturnRemembered = TRUE;
    params.fReturnConnected = TRUE;
    params.fIssueInquiry = FALSE;
    params.cTimeoutMultiplier = 0;

    BLUETOOTH_DEVICE_INFO devInfo = {};
    devInfo.dwSize = sizeof(devInfo);
    HBLUETOOTH_DEVICE_FIND hDevFind = BluetoothFindFirstDevice(&params, &devInfo);
    bool foundAirPods = false;
    if (hDevFind) {
        do {
            std::wstring name(devInfo.szName);
            if (name.find(L"AirPods") != std::wstring::npos ||
                name.find(L"AirPod")  != std::wstring::npos) {
                foundAirPods = true;
                BTH_ADDR bthAddr = bluetoothAddressToBthAddr(devInfo.Address);
                wprintf(L"\nFound AirPods: %s\n", devInfo.szName);
                probeRemoteRadioInfo(hRadio, bthAddr, devInfo.szName);
                probeSdpIoctl(hRadio, bthAddr, devInfo.szName);
            }
            devInfo = {};
            devInfo.dwSize = sizeof(devInfo);
        } while (BluetoothFindNextDevice(hDevFind, &devInfo));
        BluetoothFindDeviceClose(hDevFind);
    }
    if (!foundAirPods) {
        wprintf(L"\nNo AirPods found in paired devices — run device-targeted probes manually.\n");
    }

    BluetoothFindRadioClose(hFind);
    CloseHandle(hRadio);
    wprintf(L"\n=== Probe complete ===\n");
    return 0;
}
