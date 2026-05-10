# Windows Porting Progress Log

작성일: 2026-05-09

이 문서는 다음 세션에서 바로 이어받을 수 있도록 Windows 포팅 진행 이력, 검증 명령, 현재 막힌 지점, 다음 작업을 기록한다.

## 완료된 환경 구성

- CMake 설치 완료: `C:\Program Files\CMake\bin\cmake.exe`
- Visual Studio Build Tools 설치 완료: `C:\BuildTools`
- MSVC 확인 완료: `cl` 19.44.35226
- Qt 설치 완료: `C:\Qt\6.8.3\msvc2022_64`
- Windows SDK C++/WinRT 헤더 확인 완료:
  - `C:\Program Files (x86)\Windows Kits\10\Include\10.0.26100.0\cppwinrt`
- OpenSSL Dev 설치 완료: `C:\Program Files\OpenSSL-Win64`

## 완료된 코드 작업

- Windows 포팅 분석서 추가: `docs/windows-porting-analysis.md`
- Windows feasibility 실험 솔루션 추가: `experiments/windows-feasibility`
- .NET BLE 광고 프로브 추가 및 실행 성공
- .NET Bluetooth service/AACP UUID 프로브 추가 및 실행 성공
- .NET battery tray MVP 추가
- Linux BLE parser를 공통 C++ parser로 추출:
  - `linux/ble/bleinfo.h`
  - `linux/ble/bleadvertisementparser.h`
  - `linux/ble/bleadvertisementparser.cpp`
- 기존 `linux/ble/blemanager.*`는 Qt Bluetooth scan orchestration만 담당하도록 축소
- Windows-only CMake smoke target 추가:
  - `librepods-ble-parser-smoke`
  - `linux/tests/ble_parser_smoke.cpp`

## 검증된 결과

### .NET feasibility solution

```powershell
dotnet build .\experiments\windows-feasibility\LibrePods.WindowsFeasibility.sln
```

결과:

- 성공
- 경고 0
- 오류 0

### BLE advertisement probe

```powershell
dotnet run --project .\experiments\windows-feasibility\ble-advertisement-probe\BleAdvertisementProbe.csproj -- --seconds 12
```

결과:

- Apple manufacturer packets: 68
- Parsed AirPods packets: 10
- Windows에서 `BluetoothLEAdvertisementWatcher`로 AirPods proximity packet 수신 가능 확인

### Bluetooth service probe

```powershell
dotnet run --project .\experiments\windows-feasibility\bluetooth-service-probe\BluetoothServiceProbe.csproj
```

결과:

- paired Classic Bluetooth devices에 AirPods 표시
- AACP UUID `74ec2172-0bad-4d01-8f77-997b2be0722a`는 RFCOMM/GATT selector에서 발견되지 않음
- 결론: 제어 기능은 WinRT 고수준 API만으로는 부족하고 native SDP/socket PoC 필요

### C++ BLE parser smoke

```powershell
cmd /c "call C:\BuildTools\Common7\Tools\VsDevCmd.bat -arch=x64 && cmake -S linux -B build\windows-qt -G Ninja -DCMAKE_PREFIX_PATH=C:\Qt\6.8.3\msvc2022_64 && cmake --build build\windows-qt"
$env:Path = "C:\Qt\6.8.3\msvc2022_64\bin;$env:Path"
.\build\windows-qt\librepods-ble-parser-smoke.exe
```

결과:

- configure 성공
- build 성공
- 실행 성공

## 현재 작업 중

Windows C++/Qt BLE scanner wrapper를 추가했고 smoke target에서 실제 AirPods BLE packet 수신까지 확인했다.

완료된 내용:

- `linux/ble/iblescanner.h`
- `linux/platform/windows/windowsblescanner.h`
- `linux/platform/windows/windowsblescanner.cpp`
- `linux/platform/windows/README.md`
- `linux/tests/windows_ble_scanner_smoke.cpp`
- CMake target: `librepods-windows-ble-scanner-smoke`

검증 명령:

```powershell
cmd /c "call C:\BuildTools\Common7\Tools\VsDevCmd.bat -arch=x64 && cmake --build build\windows-qt"
$env:Path = "C:\Qt\6.8.3\msvc2022_64\bin;$env:Path"
.\build\windows-qt\librepods-windows-ble-scanner-smoke.exe
```

검증 결과:

- 15초 동안 AirPods BLE packet 12개 파싱
- C++/WinRT `BluetoothLEAdvertisementWatcher` -> `WindowsBleScanner` -> `BleAdvertisementParser` 경로가 실제 장치에서 동작
- `IBleScanner` 추상 인터페이스 추가 후 재검증 완료
- 인터페이스 경유 smoke run에서 15초 동안 AirPods BLE packet 16개 파싱
- Windows backend code를 `linux/platform/windows`로 이동
- WinRT callback에서 직접 Qt signal을 emit하지 않고 `QMetaObject::invokeMethod(..., Qt::QueuedConnection)`로 Qt object thread에 전달하도록 수정
- queued delivery 적용 후 scanner smoke에서 15초 동안 AirPods BLE packet 11개 파싱
- Windows Qt tray MVP target 추가:
  - `linux/tests/windows_tray_mvp.cpp`
  - CMake target: `librepods-windows-tray-mvp`
- tray MVP는 `WindowsBleScanner`를 `IBleScanner` 포인터로 사용하며, tray tooltip/context menu에 최신 BLE battery summary를 표시한다.
- tray MVP 빌드 성공 및 5초 launch smoke 확인 완료
- 2026-05-10: tray MVP entrypoint를 `linux/tests/windows_tray_mvp.cpp`에서 `linux/platform/windows/windowstraymain.cpp`로 이동, CMake target도 새 경로로 업데이트하고 전체 Windows smoke target 재빌드 + 5초 launch smoke 재확인 완료
- 2026-05-10: tray MVP에 minimal QML shell 연결 (옵션 B). 신규:
  - `linux/platform/windows/windowsairpodsstate.h` — `WindowsAirPodsState` QObject. 속성 이름은 Linux `Battery`/`DeviceInfo`와 같은 모양 (`leftPodLevel`, `leftPodCharging`, `leftPodAvailable`, `rightPodLevel`, …, `caseLevel`, `deviceName`, `connected`). 30초 동안 BLE packet이 안 오면 `connected=false`로 떨어지는 stale timer 포함.
  - `linux/platform/windows/qml/Tray.qml` — frameless `Qt.Tool` popover Window. tray icon 클릭으로 토글, tray geometry 기반 위치 계산.
  - `linux/platform/windows/qml/BatteryIndicator.qml` — Linux `BatteryIndicator.qml` 복사 (의존 없음). 추후 옵션 C 단계에서 통합.
  - `windowstraymain.cpp` — `QQmlApplicationEngine`으로 popover 로드, scanner.deviceFound → state.updateFromBleInfo, tray Trigger 클릭으로 popover show/hide.
  - CMake에 `Qt6::Qml`, `Qt6::Quick`, `Qt6::QuickControls2` 링크 추가, 별도 qrc로 QML 등록.
  - 빌드 성공 + 5초 launch smoke 통과 (QML 로드 stderr empty).

- 2026-05-10: native AACP SDP/socket probe 추가 (`linux/tests/windows_aacp_probe.cpp`, target `librepods-windows-aacp-probe`).
  결과 요약:
  - SDP: ✅ `WSALookupServiceBeginW(NS_BTH)` 성공 — "AAP Server", L2CAP PSM **0x1001** 발견
  - L2CAP socket(SOCK_STREAM): socket() 성공, bind()/connect() → **WSAENETDOWN(10050)** (local bind도 동일)
  - L2CAP socket(SOCK_SEQPACKET): socket() → **WSAESOCKTNOSUPPORT(10044)**
  - 결론: **Windows userspace에서 raw L2CAP 접근은 OS 레벨에서 차단됨** (원격 기기 무관)
  - 상세 결과: `experiments/windows-feasibility/RESULTS.md`

- 2026-05-10: HCI IOCTL probe 추가 (`linux/tests/windows_hci_ioctl_probe.cpp`, target `librepods-windows-hci-ioctl-probe`).
  결과 요약:
  - `IOCTL_BTH_GET_LOCAL_INFO` ✅ — Intel 어댑터, HCI 0x0B (BT 5.2)
  - `IOCTL_BTH_GET_HOST_SUPPORTED_FEATURES` ✅ — Enhanced Retransmission, Streaming Mode, LE, SCO HCI bypass
  - `IOCTL_BTH_GET_DEVICE_INFO` ✅ — 캐시된 6개 장치. AirPods: `LE_CONNECTED` 있음, `CONNECTED`(BR/EDR) 없음 → 오디오 없이는 BR/EDR 링크 없음
  - `IOCTL_BTH_SDP_CONNECT` + `SERVICE_SEARCH` + `ATTRIBUTE_SEARCH` ✅ — **전체 SDP 레코드 획득**. 디코딩: ServiceClass=AACP UUID, ProtocolDescriptorList=[L2CAP, PSM=0x1001] 재확인
  - `IOCTL_BTH_HCI_VENDOR_COMMAND` ❌ 1314 (ERROR_PRIVILEGE_NOT_HELD) — **관리자 권한으로 실행 시 접근 가능 가능성**
  - 상세: `experiments/windows-feasibility/RESULTS.md`

## 다음 세션에서 이어갈 작업

1. tray MVP를 실제로 장시간 실행하며 tooltip/context menu/popover 토글/배터리 표시를 육안 확인한다.
2. AACP 제어 채널 탐색 — 우선 순위 순:
   a. **관리자 권한으로 HCI probe 실행** → `IOCTL_BTH_HCI_VENDOR_COMMAND` 접근 여부 확인. 성공하면 raw HCI 명령 전송 가능
   b. **오디오 재생 중 AACP probe 실행** → BR/EDR ACL 링크 활성 상태에서 L2CAP 소켓/IOCTL 동작 재확인
   c. WDF kernel filter driver (개발용 test-signing 모드로 코드 서명 없이 가능)
3. AACP 제어 채널 확보 후 `WindowsAirPodsState`에 noise control / ear detection 속성 추가, 옵션 C 진입 결정.

## 주의사항

- 현재 전체 `linux` 앱은 Windows에서 빌드하지 않는다. `if(WIN32)`에서 smoke target만 구성한다.
- Linux full app은 여전히 DBus/PulseAudio/OpenSSL/pkg-config 의존성을 사용한다.
- OpenSSL 4는 CMake `FindOpenSSL`과 바로 맞물리지 않았다. Windows smoke target은 OpenSSL이 필요 없는 BLE parser만 검증한다.
- `build/windows-qt`는 생성 산출물이므로 커밋하지 않는다.
