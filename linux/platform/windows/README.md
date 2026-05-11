# Windows Platform Backend (BLE-only)

이 디렉토리는 LibrePods의 Windows 백엔드 코드입니다. **BLE-only 타협판**이며, AACP 능동 제어는 OS 차원의 한계로 동작하지 않습니다 (자세한 내용은 저장소 루트 [README](../../../README.md), [`docs/windows-porting-progress.md`](../../../docs/windows-porting-progress.md), [`experiments/windows-feasibility/RESULTS.md`](../../../experiments/windows-feasibility/RESULTS.md) 참고).

## 구성

| 파일 | 역할 |
|------|------|
| `windowsblescanner.{h,cpp}` | C++/WinRT `BluetoothLEAdvertisementWatcher`를 감싸는 `IBleScanner` 구현. WinRT 콜백을 `QMetaObject::invokeMethod` (Qt::QueuedConnection)로 Qt object thread에 다시 던진 뒤 `deviceFound(BleInfo)` 시그널을 emit. |
| `windowsairpodsstate.h` | QML facing state. 속성 이름은 Linux `Battery` / `DeviceInfo`와 동일 (`leftPodLevel`, `leftPodCharging`, `leftPodAvailable`, ..., `caseLevel`, `deviceName`, `connected`). 30초 동안 BLE packet이 안 오면 `connected=false`로 떨어지는 stale timer 포함. |
| `windowstraymain.cpp` | Tray app entrypoint. `QSystemTrayIcon` + `QQmlApplicationEngine`으로 popover 로드. 타깃: `librepods-windows-tray-mvp`. |
| `qml/Tray.qml` | Frameless `Qt.Tool` popover. 트레이 아이콘 좌표 기준으로 위치 계산. |
| `qml/BatteryIndicator.qml` | Linux `BatteryIndicator.qml`의 의존 없는 사본. |

## 동작하는 것

- AirPods proximity BLE 광고 수신 및 파싱
- 좌/우 팟 + 케이스 배터리 레벨, 충전 중 여부, 가용 여부
- 귀착용 (in-ear) 상태
- 트레이 툴팁/popover 표시
- BLE packet 끊겨도 30초 후에 자동으로 disconnected로 표시

## 동작 안 하는 것 (그리고 안 되는 이유)

- ANC/대화 모드 변경, 헤드 제스처, 보청기 등 **모든 능동 제어**
  - AACP는 L2CAP 위에서 동작하는데 Windows userspace는 raw L2CAP 소켓을 막아 놓음 (`WSAENETDOWN`)
  - SDP로 AACP UUID와 PSM=0x1001은 발견되지만 socket connect는 항상 실패
  - 관리자 권한이나 `BluetoothSetServiceState` API로 우회 시도도 모두 실패함
- AirPods 이름 변경, 펌웨어 정보 등 — 같은 이유로 불가

## 추가하지 않은 백엔드

원래 계획에 있었지만 능동 제어가 막힌 시점에 의미가 없어진 것들:

- Core Audio volume endpoint controller
- GSMTC (Global System Media Transport Controls) media session controller

이들은 AACP 제어가 살아있을 때 음악 자동 일시정지/재개 같은 컴파니언 기능을 위한 것이었습니다. AACP 자체가 막혔으니 우선순위 낮음.

## 진단 프로브

같은 빌드에서 진단 도구도 함께 생성됩니다:

| 타깃 | 용도 |
|------|------|
| `librepods-ble-parser-smoke` | BLE 파서 단위 검증 |
| `librepods-windows-ble-scanner-smoke` | C++/WinRT 스캐너 + 파서 통합 검증 (실제 AirPods 필요) |
| `librepods-windows-aacp-probe` | SDP / Winsock L2CAP 동작 확인 |
| `librepods-windows-hci-ioctl-probe` | 라디오 핸들 IOCTL 테스트 (UAC 매니페스트 내장 — 관리자로 자동 실행) |
| `librepods-windows-bredr-l2cap-probe` | BR/EDR 강제 + L2CAP 재시도 |

코드: [`linux/tests/windows_*.cpp`](../../tests/).
