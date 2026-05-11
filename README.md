# LibrePods (Windows 포팅 시도 — BLE-only 타협판)

> 이 저장소는 [`kavishdevar/librepods`](https://github.com/kavishdevar/librepods)의 포크입니다.
> Windows에서 풀 기능 AirPods 제어 클라이언트를 만들어보려고 시도했고, **OS 차원의 한계로 능동 제어(노이즈 캔슬링 전환 등) 부분은 포기**했습니다. 대신 **BLE 광고만으로 가능한 기능**(배터리, 귀착용 감지, proximity 표시)을 트레이 앱으로 정리했습니다.

![LibrePods Banner](./imgs/banner.png)

---

## TL;DR

| 항목 | 결과 |
|------|------|
| Windows에서 BLE proximity packet 수신 | ✅ 동작 |
| 트레이 아이콘 + 배터리 레벨 표시 | ✅ 동작 |
| 귀착용(in-ear) 감지 표시 | ✅ 동작 |
| L2CAP 기반 능동 제어 (ANC 모드 변경, 대화 모드, 헤드 제스처 등) | ❌ **OS 차원에서 막힘** |
| Linux/Android 풀 기능 | 원본 [`kavishdevar/librepods`](https://github.com/kavishdevar/librepods) 사용 권장 |

---

## 왜 포크했고 왜 타협했나

### 시도한 것

원본 LibrePods는 Linux와 Android에서 AirPods의 모든 비공개 기능을 잠금해제합니다. Linux 클라이언트는 Apple 독자 프로토콜(AACP, AirPods Control Protocol)을 L2CAP 소켓을 통해 직접 주고받는 방식으로 동작합니다. 이 프로젝트는 **Linux 클라이언트를 Windows로 포팅**하는 게 목표였습니다.

C++ 코어 + Qt UI는 그대로 가져올 수 있을 것 같았고, BLE 광고는 WinRT API로 받을 수 있다는 게 확인됐습니다. 문제는 **AACP 제어 채널**이었습니다.

### 무엇이 막혔나

여러 단계의 native 프로브를 작성해서 다음을 확인했습니다 (전체 결과는 [PROBE-RESULTS](./experiments/windows-feasibility/RESULTS.md)):

| 경로 | 결과 |
|------|------|
| WinRT 고수준 RFCOMM/GATT API | ❌ AACP UUID 노출 안 됨 |
| Win32 SDP (`WSALookupServiceBeginW`) | ✅ AACP 서비스 발견 (PSM=0x1001) |
| Userspace L2CAP socket connect | ❌ **`WSAENETDOWN` (모든 케이스)** |
| **로컬 L2CAP bind (원격 기기 무관)** | ❌ **`WSAENETDOWN`** |
| 관리자 권한 + `IOCTL_BTH_HCI_VENDOR_COMMAND` | ❌ admin도 부족 (`PRIVILEGE_NOT_HELD`) |
| `BluetoothSetServiceState`로 BR/EDR 강제 | ❌ `INVALID_PARAMETER` |
| `IOCTL_BTH_*` 읽기 (DEVICE_INFO, SDP_*) | ✅ 가능하지만 읽기 전용 |

**핵심**: 로컬 L2CAP bind에서도 `WSAENETDOWN`이 떨어진다는 건, 원격 AirPods의 연결 상태와 무관하게 **Windows Winsock 스택이 raw L2CAP을 일반 사용자 코드에서 막아 놨다**는 뜻입니다. 관리자 권한도 충분치 않습니다.

### 비교: 다른 플랫폼은 왜 되나

| 플랫폼 | AACP 동작 |
|--------|-----------|
| Linux (BlueZ) | **모든 어댑터에서 동작**. `AF_BLUETOOTH/SOCK_SEQPACKET/BTPROTO_L2CAP`이 ~15년 동안 표준 커널 API. 권한 문제 없음 |
| Android | 일부 기기만. Fluoride 스택 버그 ([`371713238`](https://issuetracker.google.com/issues/371713238)). Android 17에서 수정 예정. 그 외에는 root + Xposed 우회 필요 |
| Windows | **userspace에서 불가능**. Microsoft가 의도적으로 차단 |

즉 다른 사람들이 만든 Windows용 AirPods 도구들이 미흡했던 건 게으름이 아니라, Microsoft가 막아 놓은 길이라 **합법적 우회 경로 자체가 매우 좁기** 때문입니다.

### 남은 길 (이 포크에서는 채택 안 함)

1. **WSL2 + USB Bluetooth dongle passthrough** (`usbipd-win`). BlueZ를 WSL2에서 돌리고 외장 USB 동글을 통째로 넘겨주면 Linux 코드 그대로 동작합니다. 내장 어댑터는 passthrough 불가. 파워유저용.
2. **WDF Bluetooth 필터 드라이버**. 네이티브 풀 솔루션이지만 코드 서명·드라이버 라이프사이클 관리 등 부담이 큽니다.
3. **(이 포크가 채택)** **BLE-only 타협**. AirPods가 항상 송출하는 BLE proximity 광고만으로 사용자가 가장 자주 보는 정보(배터리/귀착용)는 다 됩니다. 능동 제어는 안 되지만 AirPods 자체 버튼/터치로 됩니다.

---

## Windows에서 빌드 / 실행

### 사전 요구

- Windows 10 1809+ 또는 Windows 11
- Visual Studio 2022 Build Tools (또는 Visual Studio 2022 Community 이상)
- CMake 3.16+
- Qt 6.8.x (`msvc2022_64`)
- Windows 10/11 SDK (cppwinrt 헤더 포함, 보통 자동 설치됨)

### 빌드

```powershell
# Developer Command Prompt for VS 2022 (또는 VsDevCmd.bat -arch=x64) 안에서
cmake -S linux -B build/windows -G Ninja `
  -DCMAKE_PREFIX_PATH="C:\Qt\6.8.3\msvc2022_64"
cmake --build build/windows
```

### 실행

```powershell
$env:Path = "C:\Qt\6.8.3\msvc2022_64\bin;$env:Path"
.\build\windows\librepods-windows-tray-mvp.exe
```

또는 [GitHub Actions의 최신 아티팩트](../../actions/workflows/ci-windows.yml)를 다운로드해서 압축만 풀고 `librepods-tray.exe` 실행 (Qt DLL 포함되어 있음).

### 사용법

1. AirPods를 Windows에 페어링
2. 트레이의 LibrePods 아이콘 위에 마우스를 올리면 배터리 표시
3. 클릭하면 popover 창에 좌/우/케이스 배터리 + 귀착용 상태 표시

소리 자체나 ANC 모드 변경 등은 **Windows 기본 Bluetooth 동작**과 AirPods 자체 버튼/터치로 처리됩니다.

---

## 진단 도구

`librepods-windows-bredr-l2cap-probe.exe` 같은 진단 프로브들이 같이 빌드됩니다. 본인 환경에서 위에 적힌 한계가 그대로 재현되는지 확인하거나 (혹은 새 SDK/펌웨어에서 뭔가 풀렸는지 확인하고 싶다면) 직접 돌려볼 수 있습니다.

자세한 코드와 설명: [`linux/tests/windows_*_probe.cpp`](./linux/tests/), [`docs/windows-porting-progress.md`](./docs/windows-porting-progress.md), [`experiments/windows-feasibility/RESULTS.md`](./experiments/windows-feasibility/RESULTS.md).

---

## 풀 기능을 원한다면

- **Linux**: 원본 프로젝트 [`kavishdevar/librepods`](https://github.com/kavishdevar/librepods)의 [`linux/`](https://github.com/kavishdevar/librepods/tree/main/linux) 디렉토리. 이 포크에 포함된 Linux 코드도 그대로 빌드 가능 (`linux/CMakeLists.txt`).
- **Android**: 원본 프로젝트의 [`android/`](https://github.com/kavishdevar/librepods/tree/main/android) 디렉토리.
- **Windows에서 풀 기능을 정말 원한다면**:
  - 상용 대안: [MagicPods](https://magicpods.app/) (커널 컴포넌트가 있는 유료 솔루션)
  - DIY: 위에서 언급한 WSL2 + USB Bluetooth dongle passthrough 경로

---

## 이 포크에 포함된 변경 (원본 대비)

- `linux/platform/windows/` — Windows 전용 백엔드 (C++/WinRT BLE scanner, Qt tray, QML popover)
- `linux/tests/windows_*_probe.cpp` — Win32 / Winsock / IOCTL 프로브 4종
- `linux/tests/windows_ble_scanner_smoke.cpp` — BLE 수신 검증 smoke
- `linux/tests/ble_parser_smoke.cpp` — BLE 파서 단위 검증
- `linux/CMakeLists.txt` — `if(WIN32)` 분기에서 위 타겟들만 빌드. Windows SDK cppwinrt 경로 자동 검색.
- `.github/workflows/ci-windows.yml` — Windows 빌드 CI
- `docs/windows-porting-progress.md` — 포팅 진행 이력
- `experiments/windows-feasibility/` — feasibility 실험들 (.NET 프로브, RESULTS.md)

원본의 Linux/Android 코드는 변경하지 않았습니다.

---

## License

원본과 동일하게 **GNU GPL v3.0** (또는 그 이후 버전).

```
LibrePods - AirPods liberated from Apple's ecosystem
Copyright (C) 2025 LibrePods contributors

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU General Public License as published by
the Free Software Foundation, either version 3 of the License, or
any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU General Public License for more details.
```

상세는 [LICENSE](./LICENSE) 참고.

---

## 크레딧

원본 LibrePods 프로젝트의 모든 기여자에게 감사:

- **[@kavishdevar](https://github.com/kavishdevar)** — 원본 프로젝트 메인테이너
- **[@tyalie](https://github.com/tyalie)** — AAP Protocol 첫 문서화 ([AAP-Protocol-Definition](https://github.com/tyalie/AAP-Protocol-Defintion))
- **[@rithvikvibhu](https://github.com/rithvikvibhu)** & lagrangepoint — 보청기 기능 ([gist](https://gist.github.com/rithvikvibhu/45e24bbe5ade30125f152383daf07016))
- **[@timgromeyer](https://github.com/timgromeyer)** — Linux 첫 버전
- **[@devnoname120](https://github.com/devnoname120)** — 첫 root 패치
- **[@hackclub](https://hackclub.com)** — High Seas / Low Skies 호스팅

상표·로고·브랜드 이름은 각 소유주의 자산입니다. AirPods 이미지·심볼·SF Pro 폰트는 Apple Inc. 자산입니다.
