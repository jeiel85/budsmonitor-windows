// windows_aacp_probe.cpp
//
// Native Win32 Bluetooth probe for the AirPods Control Protocol (AACP).
//
// Goal: determine whether the AACP L2CAP service can be discovered via SDP and
// whether a raw socket connection to it is possible on Windows — something that
// the WinRT high-level RFCOMM/GATT selectors could not confirm.
//
// Build: librepods-windows-aacp-probe (Windows-only, no Qt)
//
// APIs used:
//   BluetoothFindFirstDevice / BluetoothFindNextDevice  (Bthprops.lib)
//   WSALookupServiceBeginW / WSALookupServiceNextW      (ws2_32.lib)
//   socket(AF_BTH, SOCK_STREAM, BTHPROTO_L2CAP)         (ws2_32.lib)
//   socket(AF_BTH, SOCK_STREAM, BTHPROTO_RFCOMM)        (ws2_32.lib)

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

// -------------------------------------------------------------------------
// Helpers
// -------------------------------------------------------------------------

static std::wstring bthAddrToString(BLUETOOTH_ADDRESS addr)
{
    wchar_t buf[32];
    swprintf_s(buf, L"%02X:%02X:%02X:%02X:%02X:%02X",
               addr.rgBytes[5], addr.rgBytes[4], addr.rgBytes[3],
               addr.rgBytes[2], addr.rgBytes[1], addr.rgBytes[0]);
    return buf;
}

// Convert a BTH_ADDR (ULONGLONG) to the string form Winsock expects for
// BTH address context: "(XX:XX:XX:XX:XX:XX)"
static std::wstring bthAddrToContext(BTH_ADDR addr)
{
    wchar_t buf[32];
    swprintf_s(buf, L"(%02llX:%02llX:%02llX:%02llX:%02llX:%02llX)",
               (addr >> 40) & 0xFF,
               (addr >> 32) & 0xFF,
               (addr >> 24) & 0xFF,
               (addr >> 16) & 0xFF,
               (addr >> 8)  & 0xFF,
               (addr)       & 0xFF);
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
// 1. Enumerate paired Bluetooth devices
// -------------------------------------------------------------------------

struct PairedDevice {
    BLUETOOTH_DEVICE_INFO info;
};

static std::vector<PairedDevice> enumeratePairedDevices()
{
    std::vector<PairedDevice> devices;

    BLUETOOTH_DEVICE_SEARCH_PARAMS params = {};
    params.dwSize = sizeof(params);
    params.fReturnAuthenticated = TRUE;
    params.fReturnRemembered = TRUE;
    params.fReturnConnected = TRUE;
    params.fReturnUnknown = FALSE;
    params.fIssueInquiry = FALSE;
    params.cTimeoutMultiplier = 0;

    BLUETOOTH_DEVICE_INFO info = {};
    info.dwSize = sizeof(info);

    HBLUETOOTH_DEVICE_FIND hFind = BluetoothFindFirstDevice(&params, &info);
    if (!hFind) {
        wprintf(L"  BluetoothFindFirstDevice failed: 0x%08lX\n", GetLastError());
        return devices;
    }

    do {
        PairedDevice pd;
        pd.info = info;
        devices.push_back(pd);
        info = {};
        info.dwSize = sizeof(info);
    } while (BluetoothFindNextDevice(hFind, &info));

    BluetoothFindDeviceClose(hFind);
    return devices;
}

// -------------------------------------------------------------------------
// 2. SDP query via WSALookupServiceBeginW
// Returns discovered PSM/channel (0 if not found)
// -------------------------------------------------------------------------

static ULONG querySdp(BTH_ADDR devAddr, const wchar_t *devName)
{
    wprintf(L"\n  -- SDP query on %s --\n", devName);

    std::wstring context = bthAddrToContext(devAddr);

    // Buffer for results — 64 KB should be enough for most SDP records
    const DWORD bufSize = 65536;
    std::vector<BYTE> buf(bufSize, 0);
    LPWSAQUERYSET pResult = reinterpret_cast<LPWSAQUERYSET>(buf.data());

    WSAQUERYSET qs = {};
    qs.dwSize = sizeof(qs);
    qs.dwNameSpace = NS_BTH;
    qs.lpServiceClassId = const_cast<GUID *>(&AACP_UUID);
    qs.lpszContext = const_cast<wchar_t *>(context.c_str());

    HANDLE hLookup = nullptr;
    DWORD flags = LUP_FLUSHCACHE | LUP_RETURN_NAME | LUP_RETURN_ADDR
                | LUP_RETURN_TYPE | LUP_RETURN_BLOB;

    int rc = WSALookupServiceBeginW(&qs, flags, &hLookup);
    if (rc != 0) {
        int err = WSAGetLastError();
        if (err == WSASERVICE_NOT_FOUND) {
            wprintf(L"    AACP UUID not found in SDP records (WSASERVICE_NOT_FOUND)\n");
        } else {
            wprintf(L"    WSALookupServiceBeginW failed: %d\n", err);
        }
        return 0;
    }

    ULONG discoveredPsm = 0;
    bool found = false;
    while (true) {
        DWORD resultBufSize = bufSize;
        pResult->dwSize = sizeof(WSAQUERYSET);
        rc = WSALookupServiceNextW(hLookup, flags, &resultBufSize, pResult);
        if (rc != 0) {
            int err = WSAGetLastError();
            if (err == WSA_E_NO_MORE || err == WSAENOMORE) {
                if (!found)
                    wprintf(L"    No SDP records returned\n");
            } else {
                wprintf(L"    WSALookupServiceNextW error: %d\n", err);
            }
            break;
        }

        found = true;
        wprintf(L"    SDP record found!\n");
        if (pResult->lpszServiceInstanceName)
            wprintf(L"    Service name:  %s\n", pResult->lpszServiceInstanceName);
        if (pResult->lpServiceClassId)
            wprintf(L"    ClassId:       {%08lX-...}\n",
                    pResult->lpServiceClassId->Data1);

        if (pResult->dwNumberOfCsAddrs > 0 && pResult->lpcsaBuffer) {
            for (DWORD i = 0; i < pResult->dwNumberOfCsAddrs; ++i) {
                CSADDR_INFO *csa = &pResult->lpcsaBuffer[i];
                if (csa->RemoteAddr.lpSockaddr &&
                    csa->RemoteAddr.lpSockaddr->sa_family == AF_BTH)
                {
                    SOCKADDR_BTH *sa =
                        reinterpret_cast<SOCKADDR_BTH *>(csa->RemoteAddr.lpSockaddr);
                    wprintf(L"    Remote BTH addr: %012llX  port/PSM: %lu  proto: %d\n",
                            sa->btAddr, sa->port, csa->iProtocol);
                    if (sa->port != 0 && discoveredPsm == 0)
                        discoveredPsm = sa->port;
                }
            }
        } else {
            wprintf(L"    (no address entries in SDP record)\n");
        }
    }

    WSALookupServiceEnd(hLookup);
    return discoveredPsm;
}

// -------------------------------------------------------------------------
// 3. Socket connection attempts
// -------------------------------------------------------------------------

static void trySocketConnect(BTH_ADDR devAddr, const wchar_t *devName,
                              int sockType, int protocol,
                              const wchar_t *protoName, ULONG port)
{
    wprintf(L"\n  -- %s socket connect to %s (port/PSM %lu) --\n",
            protoName, devName, port);

    SOCKET s = socket(AF_BTH, sockType, protocol);
    if (s == INVALID_SOCKET) {
        wprintf(L"    socket() failed: %d\n", WSAGetLastError());
        return;
    }

    // Check socket health immediately
    {
        int soErr = 0; int soLen = sizeof(soErr);
        getsockopt(s, SOL_SOCKET, SO_ERROR, reinterpret_cast<char *>(&soErr), &soLen);
        if (soErr) wprintf(L"    SO_ERROR after socket(): %d\n", soErr);
    }

    SOCKADDR_BTH addr = {};
    addr.addressFamily = AF_BTH;
    addr.btAddr = devAddr;
    addr.serviceClassId = AACP_UUID;
    addr.port = port;

    // Non-blocking connect with short timeout via select()
    DWORD nb = 1;
    ioctlsocket(s, FIONBIO, &nb);

    int rc = connect(s, reinterpret_cast<SOCKADDR *>(&addr), sizeof(addr));
    if (rc == 0) {
        wprintf(L"    connect() succeeded immediately!\n");
    } else {
        int err = WSAGetLastError();
        if (err == WSAEWOULDBLOCK || err == WSAEINPROGRESS) {
            // Wait up to 6 seconds
            fd_set wfds;
            FD_ZERO(&wfds);
            FD_SET(s, &wfds);
            timeval tv = {6, 0};
            int sel = select(0, nullptr, &wfds, nullptr, &tv);
            if (sel > 0) {
                int soErr = 0;
                int soLen = sizeof(soErr);
                getsockopt(s, SOL_SOCKET, SO_ERROR,
                           reinterpret_cast<char *>(&soErr), &soLen);
                if (soErr == 0) {
                    wprintf(L"    connect() succeeded after select!\n");
                } else {
                    wprintf(L"    connect() failed after select: %d\n", soErr);
                }
            } else if (sel == 0) {
                wprintf(L"    connect() timed out (6 s)\n");
            } else {
                wprintf(L"    select() error: %d\n", WSAGetLastError());
            }
        } else {
            wprintf(L"    connect() failed immediately: %d\n", err);
        }
    }

    closesocket(s);
}

// Try to bind a server-side L2CAP socket locally (confirms socket type support)
static void tryL2capServerBind(int sockType, const wchar_t *typeName)
{
    wprintf(L"\n  -- Local L2CAP server bind (%s) --\n", typeName);
    SOCKET s = socket(AF_BTH, sockType, BTHPROTO_L2CAP);
    if (s == INVALID_SOCKET) {
        wprintf(L"    socket() failed: %d\n", WSAGetLastError());
        return;
    }
    wprintf(L"    socket() OK\n");

    SOCKADDR_BTH localAddr = {};
    localAddr.addressFamily = AF_BTH;
    localAddr.btAddr = 0; // any local adapter
    localAddr.port = BT_PORT_ANY;

    int rc = bind(s, reinterpret_cast<SOCKADDR *>(&localAddr), sizeof(localAddr));
    if (rc == 0) {
        wprintf(L"    bind() OK — L2CAP %s sockets supported\n", typeName);
    } else {
        wprintf(L"    bind() failed: %d\n", WSAGetLastError());
    }
    closesocket(s);
}

// -------------------------------------------------------------------------
// main
// -------------------------------------------------------------------------

int main()
{
    wprintf(L"LibrePods Windows AACP Probe\n");
    wprintf(L"AACP UUID: {74ec2172-0bad-4d01-8f77-997b2be0722a}\n\n");

    // Init Winsock
    WSADATA wsaData = {};
    if (WSAStartup(MAKEWORD(2, 2), &wsaData) != 0) {
        wprintf(L"WSAStartup failed\n");
        return 1;
    }

    // Step 1: enumerate paired devices
    wprintf(L"=== Paired Bluetooth devices ===\n");
    auto devices = enumeratePairedDevices();
    if (devices.empty()) {
        wprintf(L"  No paired devices found\n");
        WSACleanup();
        return 0;
    }

    for (const auto &pd : devices) {
        const BLUETOOTH_DEVICE_INFO &info = pd.info;
        std::wstring addr = bthAddrToString(info.Address);
        wprintf(L"  %s  [%s]  connected=%d  authenticated=%d  remembered=%d\n",
                info.szName, addr.c_str(),
                info.fConnected, info.fAuthenticated, info.fRemembered);
    }

    // Step 2 + 3: for each AirPods-looking device run SDP + socket probes
    bool probed = false;
    for (const auto &pd : devices) {
        const BLUETOOTH_DEVICE_INFO &info = pd.info;
        // Heuristic: AirPods show up as "AirPods" or contain "AirPods" / "Beats"
        std::wstring name(info.szName);
        bool looksLikeAirPods = name.find(L"AirPods") != std::wstring::npos
                              || name.find(L"Beats")   != std::wstring::npos
                              || name.find(L"AirPod")  != std::wstring::npos;

        if (!looksLikeAirPods) continue;
        probed = true;

        wprintf(L"\n=== Probing device: %s ===\n", info.szName);
        BTH_ADDR bthAddr = bluetoothAddressToBthAddr(info.Address);

        // fConnected reflects BR/EDR (Classic BT) status from the host stack.
        // AACP runs over L2CAP which needs a BR/EDR ACL link — when this is FALSE
        // the socket probes below will almost certainly fail with WSAENETDOWN.
        if (!info.fConnected) {
            wprintf(L"\n  NOTE: fConnected=FALSE — no active BR/EDR (Classic BT) link.\n");
            wprintf(L"  AACP uses L2CAP which requires BR/EDR. Socket probes below will\n");
            wprintf(L"  likely fail with WSAENETDOWN until a BR/EDR ACL link is active.\n");
            wprintf(L"  --> Set AirPods as audio output, play audio, then run:\n");
            wprintf(L"      librepods-windows-bredr-l2cap-probe.exe\n\n");
        } else {
            wprintf(L"\n  fConnected=TRUE — BR/EDR ACL link may be active.\n\n");
        }

        // SDP query — returns discovered PSM (0 if not found)
        ULONG psm = querySdp(bthAddr, info.szName);

        // L2CAP STREAM: PSM=0 (stack derives from SDP)
        trySocketConnect(bthAddr, info.szName,
                         SOCK_STREAM, BTHPROTO_L2CAP, L"L2CAP/STREAM(psm=0)", 0);

        // L2CAP SEQPACKET: packet-oriented (correct type per Bluetooth spec)
        trySocketConnect(bthAddr, info.szName,
                         SOCK_SEQPACKET, BTHPROTO_L2CAP, L"L2CAP/SEQPACKET(psm=0)", 0);

        if (psm != 0) {
            wprintf(L"\n  Retrying with discovered PSM %lu (0x%04lX)\n", psm, psm);
            trySocketConnect(bthAddr, info.szName,
                             SOCK_STREAM, BTHPROTO_L2CAP,
                             L"L2CAP/STREAM(psm=discovered)", psm);
            trySocketConnect(bthAddr, info.szName,
                             SOCK_SEQPACKET, BTHPROTO_L2CAP,
                             L"L2CAP/SEQPACKET(psm=discovered)", psm);
        }

        // RFCOMM as control (expect failure; confirms proto discrimination)
        trySocketConnect(bthAddr, info.szName,
                         SOCK_STREAM, BTHPROTO_RFCOMM, L"RFCOMM(psm=0)", 0);
    }

    // Local bind test — checks whether L2CAP socket types work at all
    wprintf(L"\n=== Local L2CAP bind tests ===\n");
    tryL2capServerBind(SOCK_STREAM,    L"SOCK_STREAM");
    tryL2capServerBind(SOCK_SEQPACKET, L"SOCK_SEQPACKET");

    if (!probed) {
        wprintf(L"\nNo AirPods/Beats device found in paired list. "
                L"Pair your AirPods and run again.\n");
    }

    wprintf(L"\n=== Probe complete ===\n");
    WSACleanup();
    return 0;
}
