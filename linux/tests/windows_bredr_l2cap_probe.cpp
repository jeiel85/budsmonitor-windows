// windows_bredr_l2cap_probe.cpp
//
// Interactive probe: establishes a BR/EDR (Classic BT) ACL link via audio,
// then retries L2CAP socket connection to the AACP PSM on AirPods.
//
// Background
// ----------
// The AACP control service (UUID 74ec2172-..., PSM 0x1001) lives on top of
// Classic Bluetooth L2CAP.  Windows only creates a BR/EDR ACL link to the
// AirPods when they are active as an audio output device (A2DP/HFP).
// Without that link, even raw socket() / bind() calls return WSAENETDOWN.
//
// How to use
// ----------
//   1. Run this exe.
//   2. When prompted: open Sound settings, set AirPods as the default output,
//      and start playing audio (music, video, etc.).
//   3. Press ENTER — the probe re-checks the BDIF_CONNECTED flag and then
//      tries L2CAP socket with PSM 0x1001.
//
// Build: librepods-windows-bredr-l2cap-probe (no Qt)
// Links: Bthprops.lib ws2_32.lib
// Include: shared/bthioctl.h (Windows 10 SDK 26100+)

#define WIN32_LEAN_AND_MEAN
#define NTDDI_VERSION 0x06020000   // Win8+
#define _WIN32_WINNT  0x0602
#ifndef UNICODE
#define UNICODE
#endif

#include <windows.h>
#include <winioctl.h>
#include <winsock2.h>
#include <ws2bth.h>
#include <bluetoothapis.h>
#include <bthsdpdef.h>
#include <bthdef.h>
#include <bthioctl.h>

#include <cstdio>
#include <cstring>
#include <vector>
#include <string>

// AACP service UUID: {74ec2172-0bad-4d01-8f77-997b2be0722a}
static const GUID AACP_UUID = {
    0x74ec2172, 0x0bad, 0x4d01,
    {0x8f, 0x77, 0x99, 0x7b, 0x2b, 0xe0, 0x72, 0x2a}
};

// Standard Bluetooth audio service UUIDs (used to force-enable BR/EDR ACL)
//   A2DP Sink (AudioSink, what AirPods present): 0000110B-0000-1000-8000-00805F9B34FB
//   Hands-Free                                  : 0000111E-0000-1000-8000-00805F9B34FB
//   Headset                                     : 00001108-0000-1000-8000-00805F9B34FB
static const GUID kSvcA2dpSink = {
    0x0000110B, 0x0000, 0x1000,
    {0x80, 0x00, 0x00, 0x80, 0x5F, 0x9B, 0x34, 0xFB}
};
static const GUID kSvcHandsFree = {
    0x0000111E, 0x0000, 0x1000,
    {0x80, 0x00, 0x00, 0x80, 0x5F, 0x9B, 0x34, 0xFB}
};
static const GUID kSvcHeadset = {
    0x00001108, 0x0000, 0x1000,
    {0x80, 0x00, 0x00, 0x80, 0x5F, 0x9B, 0x34, 0xFB}
};

// BDIF flag constants (prefixed to avoid conflict with bthdef.h)
static const DWORD kBDIF_CONNECTED    = 0x00000020;  // Classic BT ACL link active
static const DWORD kBDIF_LE_CONNECTED = 0x04000000;  // BLE link active

// AACP PSM (confirmed via SDP in prior probes)
static const ULONG kAACPPsm = 0x1001;

// -------------------------------------------------------------------------
// Helpers
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

static BTH_ADDR toAddr(BLUETOOTH_ADDRESS a)
{
    BTH_ADDR r = 0;
    for (int i = 0; i < 6; ++i)
        r |= (BTH_ADDR)a.rgBytes[i] << (i * 8);
    return r;
}

static std::wstring toContext(BTH_ADDR addr)
{
    wchar_t buf[32];
    swprintf_s(buf, L"(%02llX:%02llX:%02llX:%02llX:%02llX:%02llX)",
               (addr >> 40) & 0xFF, (addr >> 32) & 0xFF,
               (addr >> 24) & 0xFF, (addr >> 16) & 0xFF,
               (addr >> 8)  & 0xFF, (addr)       & 0xFF);
    return buf;
}

// -------------------------------------------------------------------------
// Radio handle
// -------------------------------------------------------------------------
static HANDLE openRadio()
{
    BLUETOOTH_FIND_RADIO_PARAMS rfp = { sizeof(rfp) };
    HANDLE hRadio = INVALID_HANDLE_VALUE;
    HBLUETOOTH_RADIO_FIND hFind = BluetoothFindFirstRadio(&rfp, &hRadio);
    if (!hFind) {
        wprintf(L"  BluetoothFindFirstRadio failed: %lu\n", GetLastError());
        return INVALID_HANDLE_VALUE;
    }
    BluetoothFindRadioClose(hFind);
    return hRadio;
}

// -------------------------------------------------------------------------
// BDIF flag check via IOCTL_BTH_GET_DEVICE_INFO
// -------------------------------------------------------------------------
struct BdifResult {
    bool  found;
    DWORD flags;
    bool  brEdrConnected;
    bool  leConnected;
};

static BdifResult checkBdif(HANDLE hRadio, BTH_ADDR devAddr)
{
    BdifResult r = {false, 0, false, false};

    const ULONG maxDev = 32;
    const DWORD bufSz  = sizeof(BTH_DEVICE_INFO_LIST)
                       + (maxDev - 1) * sizeof(BTH_DEVICE_INFO);
    std::vector<BYTE> buf(bufSz, 0);
    auto *list = reinterpret_cast<BTH_DEVICE_INFO_LIST *>(buf.data());
    list->numOfDevices = maxDev;

    DWORD returned = 0;
    if (!DeviceIoControl(hRadio, IOCTL_BTH_GET_DEVICE_INFO,
                         nullptr, 0, list, bufSz, &returned, nullptr)) {
        wprintf(L"    IOCTL_BTH_GET_DEVICE_INFO failed: %lu\n", GetLastError());
        return r;
    }

    for (ULONG i = 0; i < list->numOfDevices; ++i) {
        if (list->deviceList[i].address == devAddr) {
            r.found          = true;
            r.flags          = list->deviceList[i].flags;
            r.brEdrConnected = (r.flags & kBDIF_CONNECTED)    != 0;
            r.leConnected    = (r.flags & kBDIF_LE_CONNECTED)  != 0;
            return r;
        }
    }
    return r;
}

static void printBdif(const BdifResult &r)
{
    if (!r.found) {
        wprintf(L"    (device not found in cached device list)\n");
        return;
    }
    wprintf(L"    flags=0x%08lX  BR/EDR_CONNECTED=%d  LE_CONNECTED=%d\n",
            r.flags,
            r.brEdrConnected ? 1 : 0,
            r.leConnected    ? 1 : 0);
}

// -------------------------------------------------------------------------
// IOCTL_BTH_GET_RADIO_INFO — returns LMP info; only succeeds when the
// BR/EDR ACL link to the remote device is active.
// -------------------------------------------------------------------------
static bool checkRemoteRadioInfo(HANDLE hRadio, BTH_ADDR devAddr)
{
    wprintf(L"\n  -- IOCTL_BTH_GET_RADIO_INFO (needs active BR/EDR ACL) --\n");

    BTH_RADIO_INFO info = {};
    DWORD returned = 0;
    BOOL ok = DeviceIoControl(hRadio, IOCTL_BTH_GET_RADIO_INFO,
                              &devAddr, sizeof(devAddr),
                              &info, sizeof(info),
                              &returned, nullptr);
    if (!ok) {
        DWORD err = GetLastError();
        if (err == ERROR_DEVICE_NOT_CONNECTED || err == 1167)
            wprintf(L"    FAILED %lu (ERROR_DEVICE_NOT_CONNECTED) "
                    L"— BR/EDR ACL link absent\n", err);
        else
            wprintf(L"    FAILED: %lu\n", err);
        return false;
    }

    wprintf(L"    OK — lmpVersion=0x%02X  mfg=0x%04X  features=0x%016llX\n",
            info.lmpVersion, info.mfg, info.lmpSupportedFeatures);
    wprintf(L"    ==> BR/EDR ACL link IS active!\n");
    return true;
}

// -------------------------------------------------------------------------
// SDP query via WSALookupServiceBeginW — returns AACP PSM (or 0)
// -------------------------------------------------------------------------
static ULONG querySdpPsm(BTH_ADDR devAddr)
{
    wprintf(L"\n  -- SDP query for AACP PSM --\n");

    std::wstring ctx = toContext(devAddr);

    const DWORD bufSize = 65536;
    std::vector<BYTE> buf(bufSize, 0);
    auto *pResult = reinterpret_cast<LPWSAQUERYSET>(buf.data());

    WSAQUERYSET qs = {};
    qs.dwSize           = sizeof(qs);
    qs.dwNameSpace      = NS_BTH;
    qs.lpServiceClassId = const_cast<GUID *>(&AACP_UUID);
    qs.lpszContext      = const_cast<wchar_t *>(ctx.c_str());

    HANDLE hLookup = nullptr;
    DWORD flags = LUP_FLUSHCACHE | LUP_RETURN_NAME | LUP_RETURN_ADDR | LUP_RETURN_BLOB;

    if (WSALookupServiceBeginW(&qs, flags, &hLookup) != 0) {
        int err = WSAGetLastError();
        if (err == WSASERVICE_NOT_FOUND)
            wprintf(L"    AACP UUID not found in SDP (WSASERVICE_NOT_FOUND)\n");
        else
            wprintf(L"    WSALookupServiceBeginW failed: %d\n", err);
        return 0;
    }

    ULONG psm = 0;
    while (true) {
        DWORD sz = bufSize;
        pResult->dwSize = sizeof(WSAQUERYSET);
        int rc = WSALookupServiceNextW(hLookup, flags, &sz, pResult);
        if (rc != 0) break;

        if (pResult->lpszServiceInstanceName)
            wprintf(L"    Service: %s\n", pResult->lpszServiceInstanceName);

        if (pResult->dwNumberOfCsAddrs > 0 && pResult->lpcsaBuffer) {
            for (DWORD i = 0; i < pResult->dwNumberOfCsAddrs; ++i) {
                CSADDR_INFO *csa = &pResult->lpcsaBuffer[i];
                if (csa->RemoteAddr.lpSockaddr &&
                    csa->RemoteAddr.lpSockaddr->sa_family == AF_BTH) {
                    auto *sa = reinterpret_cast<SOCKADDR_BTH *>(
                                   csa->RemoteAddr.lpSockaddr);
                    wprintf(L"    PSM=0x%04lX (%lu)  proto=%d\n",
                            sa->port, sa->port, csa->iProtocol);
                    if (sa->port && !psm)
                        psm = sa->port;
                }
            }
        }
    }
    WSALookupServiceEnd(hLookup);

    if (!psm) wprintf(L"    No PSM found — will fall back to 0x%04X\n", kAACPPsm);
    return psm;
}

// -------------------------------------------------------------------------
// L2CAP socket connect attempt with 8-second timeout
// -------------------------------------------------------------------------
static void tryL2cap(BTH_ADDR devAddr, int sockType,
                     const wchar_t *typeName, ULONG psm)
{
    wprintf(L"\n    socket(AF_BTH, %s, BTHPROTO_L2CAP) PSM=0x%04lX\n",
            typeName, psm);

    SOCKET s = socket(AF_BTH, sockType, BTHPROTO_L2CAP);
    if (s == INVALID_SOCKET) {
        wprintf(L"      socket() failed: %d\n", WSAGetLastError());
        return;
    }
    wprintf(L"      socket() OK\n");

    SOCKADDR_BTH addr = {};
    addr.addressFamily  = AF_BTH;
    addr.btAddr         = devAddr;
    addr.serviceClassId = AACP_UUID;
    addr.port           = psm;

    DWORD nb = 1;
    ioctlsocket(s, FIONBIO, &nb);

    int rc = connect(s, reinterpret_cast<SOCKADDR *>(&addr), sizeof(addr));
    if (rc == 0) {
        wprintf(L"      connect() SUCCEEDED immediately!\n");
    } else {
        int err = WSAGetLastError();
        if (err == WSAEWOULDBLOCK || err == WSAEINPROGRESS) {
            fd_set wfds;
            FD_ZERO(&wfds);
            FD_SET(s, &wfds);
            timeval tv = {8, 0};
            int sel = select(0, nullptr, &wfds, nullptr, &tv);
            if (sel > 0) {
                int soErr = 0, soLen = sizeof(soErr);
                getsockopt(s, SOL_SOCKET, SO_ERROR,
                           reinterpret_cast<char *>(&soErr), &soLen);
                if (soErr == 0)
                    wprintf(L"      connect() SUCCEEDED after select!\n");
                else
                    wprintf(L"      connect() FAILED after select: %d\n", soErr);
            } else if (sel == 0) {
                wprintf(L"      connect() timed out (8 s)\n");
            } else {
                wprintf(L"      select() error: %d\n", WSAGetLastError());
            }
        } else {
            wprintf(L"      connect() FAILED immediately: %d", err);
            if (err == WSAENETDOWN)
                wprintf(L" (WSAENETDOWN — OS blocks raw L2CAP, or BR/EDR ACL absent)");
            else if (err == WSAESOCKTNOSUPPORT)
                wprintf(L" (WSAESOCKTNOSUPPORT — socket type not supported)");
            else if (err == WSAETIMEDOUT)
                wprintf(L" (WSAETIMEDOUT)");
            wprintf(L"\n");
        }
    }
    closesocket(s);
}

// -------------------------------------------------------------------------
// main
// -------------------------------------------------------------------------
int main()
{
    wprintf(L"LibrePods Windows BR/EDR + L2CAP Interactive Probe\n");
    wprintf(L"AACP UUID: {74ec2172-0bad-4d01-8f77-997b2be0722a}  "
            L"PSM: 0x%04X\n\n", kAACPPsm);

    // Init Winsock
    WSADATA wsaData = {};
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        wprintf(L"WSAStartup failed\n");
        return 1;
    }

    // Open radio handle
    HANDLE hRadio = openRadio();
    if (hRadio == INVALID_HANDLE_VALUE) {
        WSACleanup();
        return 1;
    }

    // Enumerate paired AirPods-like devices
    BLUETOOTH_DEVICE_SEARCH_PARAMS params = {};
    params.dwSize             = sizeof(params);
    params.fReturnAuthenticated = TRUE;
    params.fReturnRemembered  = TRUE;
    params.fReturnConnected   = TRUE;
    params.fIssueInquiry      = FALSE;
    params.cTimeoutMultiplier = 0;

    BLUETOOTH_DEVICE_INFO devInfo = {};
    devInfo.dwSize = sizeof(devInfo);

    HBLUETOOTH_DEVICE_FIND hDevFind = BluetoothFindFirstDevice(&params, &devInfo);
    if (!hDevFind) {
        wprintf(L"BluetoothFindFirstDevice failed: %lu\n", GetLastError());
        CloseHandle(hRadio);
        WSACleanup();
        return 1;
    }

    struct Target {
        BTH_ADDR              addr;
        std::wstring          name;
        BLUETOOTH_DEVICE_INFO bdi;   // full record for BluetoothSetServiceState()
    };
    std::vector<Target> targets;

    do {
        std::wstring name(devInfo.szName);
        if (name.find(L"AirPods") != std::wstring::npos ||
            name.find(L"AirPod")  != std::wstring::npos ||
            name.find(L"Beats")   != std::wstring::npos) {
            targets.push_back({ toAddr(devInfo.Address), name, devInfo });
        }
        devInfo = {};
        devInfo.dwSize = sizeof(devInfo);
    } while (BluetoothFindNextDevice(hDevFind, &devInfo));

    BluetoothFindDeviceClose(hDevFind);

    if (targets.empty()) {
        wprintf(L"No AirPods/Beats device found in paired list.\n");
        wprintf(L"Pair your AirPods first, then run again.\n");
        CloseHandle(hRadio);
        WSACleanup();
        return 0;
    }

    for (auto &t : targets) {
        wprintf(L"\n=== Target: %s  (%s) ===\n",
                t.name.c_str(), bthAddrStr(t.addr).c_str());

        // ------------------------------------------------------------------
        // Step 1 — Initial BDIF check
        // ------------------------------------------------------------------
        wprintf(L"\n  [Step 1] Initial connection status (IOCTL_BTH_GET_DEVICE_INFO):\n");
        BdifResult bdif = checkBdif(hRadio, t.addr);
        printBdif(bdif);

        // ------------------------------------------------------------------
        // Step 2 — Prompt if BR/EDR is not yet active
        // ------------------------------------------------------------------
        if (!bdif.brEdrConnected) {
            wprintf(L"\n  *** BR/EDR (Classic Bluetooth) ACL link is NOT active.\n");
            if (bdif.leConnected)
                wprintf(L"  *** Device is BLE-connected only.\n");

            // ------------------------------------------------------------------
            // Try to force a BR/EDR ACL by enabling audio services on the device.
            // BluetoothSetServiceState(BLUETOOTH_SERVICE_ENABLE) creates a
            // connection to the named profile, which brings up the BR/EDR link.
            // We try A2DP Sink first (AirPods are an A2DP sink), then HFP, HSP.
            // ------------------------------------------------------------------
            wprintf(L"\n  [Step 2a] Forcing BR/EDR link via BluetoothSetServiceState:\n");

            struct SvcAttempt { const wchar_t *name; const GUID *guid; };
            SvcAttempt attempts[] = {
                {L"A2DP Sink (0x110B)", &kSvcA2dpSink},
                {L"Hands-Free (0x111E)", &kSvcHandsFree},
                {L"Headset    (0x1108)", &kSvcHeadset},
            };
            for (auto &a : attempts) {
                DWORD r = BluetoothSetServiceState(hRadio, &t.bdi, a.guid,
                                                    BLUETOOTH_SERVICE_ENABLE);
                wprintf(L"    enable %s -> %lu", a.name, r);
                if (r == ERROR_SUCCESS)             wprintf(L" (OK)");
                else if (r == ERROR_SERVICE_DOES_NOT_EXIST) wprintf(L" (not advertised by device)");
                else if (r == 0x10D)                wprintf(L" (ERROR_SERVICE_NOT_FOUND)");
                wprintf(L"\n");
            }

            wprintf(L"\n  [Step 2b] Polling BDIF every 2 seconds (60-second timeout)...\n");
            wprintf(L"  (No GUI/audio action needed — service-enable above should trigger ACL.)\n\n");
            fflush(stdout);

            for (int i = 0; i < 30; ++i) {
                Sleep(2000);
                BdifResult c = checkBdif(hRadio, t.addr);
                wprintf(L"    [%3ds] flags=0x%08lX  BR/EDR=%d  LE=%d\n",
                        (i + 1) * 2, c.flags,
                        c.brEdrConnected ? 1 : 0,
                        c.leConnected    ? 1 : 0);
                fflush(stdout);
                if (c.brEdrConnected) {
                    bdif = c;
                    wprintf(L"    --> BR/EDR ACL link came UP after %d seconds!\n",
                            (i + 1) * 2);
                    break;
                }
            }
            if (!bdif.brEdrConnected) {
                wprintf(L"    --> Timeout. BR/EDR did NOT come up.\n");
                wprintf(L"    --> Proceeding anyway to capture full error details.\n");
            }
        } else {
            wprintf(L"    --> BR/EDR ACL link already ACTIVE, no action needed.\n");
        }

        // ------------------------------------------------------------------
        // Step 3 — IOCTL_BTH_GET_RADIO_INFO (LMP-level check, needs ACL)
        // ------------------------------------------------------------------
        bool radioOk = checkRemoteRadioInfo(hRadio, t.addr);

        // ------------------------------------------------------------------
        // Step 4 — SDP query to confirm PSM
        // ------------------------------------------------------------------
        ULONG psm = querySdpPsm(t.addr);
        if (psm == 0) {
            wprintf(L"    Falling back to well-known PSM 0x%04X\n", kAACPPsm);
            psm = kAACPPsm;
        }

        // ------------------------------------------------------------------
        // Step 5 — L2CAP socket connect attempts
        // ------------------------------------------------------------------
        wprintf(L"\n  [Step 5] L2CAP socket attempts:\n");

        // SOCK_STREAM with discovered PSM
        tryL2cap(t.addr, SOCK_STREAM,    L"SOCK_STREAM",    psm);

        // SOCK_SEQPACKET with discovered PSM (correct Bluetooth spec type)
        tryL2cap(t.addr, SOCK_SEQPACKET, L"SOCK_SEQPACKET", psm);

        // SOCK_STREAM with PSM=0 (let Winsock resolve via AACP UUID in SOCKADDR_BTH)
        tryL2cap(t.addr, SOCK_STREAM,    L"SOCK_STREAM",    0);

        wprintf(L"\n  --- Summary ---\n");
        wprintf(L"  BR/EDR ACL active (BDIF):       %s\n",
                bdif.brEdrConnected ? L"YES" : L"NO");
        wprintf(L"  IOCTL_BTH_GET_RADIO_INFO:       %s\n",
                radioOk ? L"OK (link confirmed)" : L"FAILED");
        wprintf(L"  SDP PSM:                        0x%04lX\n", psm);
    }

    CloseHandle(hRadio);
    WSACleanup();
    wprintf(L"\n=== Probe complete ===\n");
    return 0;
}
