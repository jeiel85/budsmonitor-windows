# LibrePods Windows Porting Analysis

작성일: 2026-05-09

목표: 다음 작업 세션에서 바로 PoC와 리팩터링을 시작할 수 있도록 LibrePods의 Windows 포팅 범위, 위험 요소, 필요한 스택, 구현 경로, 작업 순서를 분해한다.

## 결론 요약

Windows 포팅은 가능하지만, 단순한 재빌드가 아니라 OS 백엔드 포팅이다. 현재 Linux 앱은 Qt/QML UI는 비교적 이식 가능하지만 Bluetooth 연결 감시, AACP L2CAP 통신, 오디오 프로필 제어, 미디어 세션 제어, 자동 시작, 절전 이벤트 처리가 Linux 사용자 공간 기술에 강하게 묶여 있다.

가장 현실적인 경로는 다음 순서다.

1. Windows에서 BLE 광고를 읽어 AirPods 배터리/상태 표시 PoC를 만든다.
2. 현재 `linux` 앱을 플랫폼 독립 코어와 플랫폼 백엔드로 쪼갠다.
3. Windows BLE/트레이/자동 시작/절전/오디오/미디어 백엔드를 하나씩 붙인다.
4. AACP 제어 채널을 Windows에서 열 수 있는지 검증한다.
5. AACP가 가능하면 노이즈 컨트롤/대화 인식/이어 감지 제어를 구현한다. 불가능하면 Windows 버전은 배터리 중심 앱으로 범위를 줄인다.

핵심 리스크는 AACP 수송 계층이다. Linux는 `QBluetoothSocket(QBluetoothServiceInfo::L2capProtocol)`로 AirPods의 `74ec2172-0bad-4d01-8f77-997b2be0722a` 서비스에 붙는다. Windows 공식 Bluetooth socket 문서는 RFCOMM 중심이고, BLE 광고/GATT는 WinRT로 가능하므로, 제어 채널은 반드시 별도 PoC로 확인해야 한다.

## 현재 코드 구조

### Android

경로: `android/`

Android 앱은 Kotlin/Compose, Android Bluetooth API, Xposed/root 훅, 네이티브 C++ 코드가 섞여 있다. Windows 포팅의 직접 기반으로 쓰기 어렵다.

관련 파일:

- `android/app/src/main/java/me/kavishdevar/librepods/bluetooth/AACPManager.kt`
- `android/app/src/main/java/me/kavishdevar/librepods/bluetooth/ATTManager.kt`
- `android/app/src/main/java/me/kavishdevar/librepods/services/AirPodsService.kt`
- `android/app/src/main/cpp/l2c_fcr_hook.cpp`
- `android/app/src/main/cpp/bluetooth_socket.cpp`

가져올 수 있는 것:

- AACP opcode/packet 해석 로직
- ATT handle 목록과 hearing aid 관련 실험 코드
- Bluetooth stack 우회가 왜 필요한지에 대한 힌트

가져오기 어려운 것:

- Android service/receiver/permission 모델
- Xposed/root 훅
- Android BluetoothSocket 내부 생성자 우회
- 시스템 앱/루트 모듈 관련 기능

### Linux

경로: `linux/`

Windows 포팅의 기준점은 Linux 앱이다. Qt/QML UI, 패킷 정의, 배터리 파서, 일부 Bluetooth 파서는 재사용 가능하다.

주요 파일:

- `linux/main.cpp`: 앱 오케스트레이션, AirPods AACP socket 연결, packet parse/send, phone relay, settings
- `linux/Main.qml`: 메인 UI
- `linux/airpods_packets.h`: AirPods packet constants, control command packet builders
- `linux/battery.hpp`: 배터리 packet parser
- `linux/eardetection.hpp`: 이어 감지 parser/state
- `linux/deviceinfo.hpp`: QML에 노출되는 device state
- `linux/ble/blemanager.cpp`: BLE advertisement scan/parser
- `linux/ble/bleutils.cpp`: IRK/RPA 검증, AES decrypt
- `linux/BluetoothMonitor.cpp`: BlueZ DBus 연결/해제 감시
- `linux/media/mediacontroller.cpp`: MPRIS/PulseAudio 기반 media/audio 제어
- `linux/media/pulseaudiocontroller.cpp`: PulseAudio card/sink/profile 제어
- `linux/trayiconmanager.cpp`: Qt tray icon
- `linux/autostartmanager.hpp`: Linux desktop autostart
- `linux/systemsleepmonitor.hpp`: logind DBus sleep/wake monitor
- `linux/CMakeLists.txt`: Qt6 Bluetooth/DBus/OpenSSL/PulseAudio 의존성

## 플랫폼 의존성 지도

### 재사용 가능성이 높은 영역

이 영역은 Windows 빌드에서도 거의 그대로 유지하거나 작은 수정만으로 사용할 수 있다.

- QML UI: `Main.qml`, `BatteryIndicator.qml`, `PodColumn.qml`, `SegmentedControl.qml`
- Qt tray UI의 상위 개념: `trayiconmanager.*`
- 패킷 상수와 생성: `airpods_packets.h`, `BasicControlCommand.hpp`
- 상태 모델: `deviceinfo.hpp`, `battery.hpp`, `eardetection.hpp`
- BLE advertisement payload 파싱: `blemanager.cpp` 내부 parser
- BLE cryptography: `bleutils.cpp`
- settings 저장: `QSettings`
- single instance IPC: `QLocalServer`, `QLocalSocket`

### Linux 전용이라 대체해야 하는 영역

- BlueZ DBus 감시: `BluetoothMonitor.cpp`
- DBus/MPRIS media player 제어: `media/mediacontroller.cpp`, `media/playerstatuswatcher.cpp`
- PulseAudio profile/sink 제어: `media/pulseaudiocontroller.cpp`
- `bluetoothctl connect/disconnect`: `main.cpp`
- `systemctl --user restart wireplumber`: `media/mediacontroller.cpp`
- logind sleep/wake: `systemsleepmonitor.hpp`
- `.desktop` autostart: `autostartmanager.hpp`
- Linux install targets and desktop/icon install rules: `CMakeLists.txt`

### 불확실하거나 고위험인 영역

- Windows에서 AACP L2CAP socket 연결
- Windows에서 AirPods Device ID/vendor identity를 Apple처럼 노출
- Windows에서 AirPods custom ATT handle에 raw ATT로 접근
- Hearing Aid 관련 기능
- multi-device takeover/handoff

## 기능별 포팅 가능성

### Battery status from BLE advertisements

가능성: 높음

현재 구현:

- Linux는 `QBluetoothDeviceDiscoveryAgent::LowEnergyMethod`로 스캔한다.
- Apple manufacturer data `0x004C`가 있고 payload 첫 byte가 `0x07`이면 AirPods proximity packet으로 해석한다.
- `data[3..4]` 모델, `data[5]` status, `data[6]` pod battery, `data[7]` case battery/charging flags, `data[8]` lid, `data[9]` color, `data[10]` connection state를 읽는다.
- Magic pairing으로 얻은 IRK/ENC key가 있으면 RPA 검증과 encrypted payload decrypt를 수행한다.

Windows 구현 후보:

- WinRT `Windows.Devices.Bluetooth.Advertisement.BluetoothLEAdvertisementWatcher`
- `BluetoothLEAdvertisementReceivedEventArgs.Advertisement.ManufacturerData`
- Qt Bluetooth 유지 가능성도 있지만, Windows에서는 Qt Bluetooth backend 한계가 있을 수 있으므로 WinRT 직접 호출을 1순위로 둔다.

작업:

1. Windows 콘솔 PoC에서 BLE watcher 시작.
2. manufacturer id `0x004C`만 필터링.
3. 기존 `BleInfo` parser를 platform-neutral 함수로 분리.
4. AirPods 케이스 열기/닫기, 이어 착용 상태, 충전 상태를 로그로 검증.
5. QML device state에 연결.

완료 기준:

- AirPods 케이스 open 상태에서 모델과 배터리 값 표시.
- 이어 착용/분리 상태 변화가 3초 이내 반영.
- Windows tray tooltip 또는 QML UI에 battery status 표시.

### AACP connection and control packets

가능성: 중간에서 낮음, 가장 중요한 선행 검증 필요

현재 구현:

- `main.cpp`에서 `QBluetoothSocket(QBluetoothServiceInfo::L2capProtocol)` 생성.
- AirPods address와 UUID `74ec2172-0bad-4d01-8f77-997b2be0722a`로 `connectToService`.
- 연결 후 `AirPodsPackets::Connection::HANDSHAKE` 전송.
- ACK 이후 `SET_SPECIFIC_FEATURES`, `REQUEST_NOTIFICATIONS` 전송.
- 이후 control command packet을 같은 socket에 write.

중요 packet:

- handshake: `00000400010002000000000000000000`
- set features: `040004004d00d700000000000000`
- request notifications: `040004000f00ffffffffff`
- noise control header: `0400040009000d`
- conversational awareness control id: `0x28`
- one bud ANC control id: `0x1B`
- hearing aid control id: `0x2C`

Windows 구현 후보:

1. Qt Bluetooth 그대로 시도
   - 장점: 코드 변경 최소.
   - 단점: Qt Windows Bluetooth backend가 Classic L2CAP client를 기대대로 지원하지 않을 가능성이 큼.

2. WinSock Bluetooth
   - `AF_BTH` socket API 사용.
   - Microsoft 문서상 공식적으로 다루는 protocol은 RFCOMM 중심이라 custom L2CAP service connect가 가능한지 확인 필요.
   - 성공하면 C++/Qt와 붙이기 쉽다.

3. WinRT Bluetooth API
   - BLE advertisement/GATT는 강함.
   - AACP가 BLE GATT characteristic이 아니라 Classic/BREDR L2CAP service라면 적합하지 않을 수 있다.

4. Native helper service/driver
   - 일반 앱 API로 안 되면 마지막 선택지.
   - 난이도와 유지 비용이 매우 큼.

PoC 0:

- Windows에서 paired AirPods의 SDP/service UUID를 열람할 수 있는지 확인한다.
- `74ec2172-0bad-4d01-8f77-997b2be0722a` service가 Windows enumeration에 보이는지 확인한다.
- 보이면 Qt `QBluetoothServiceDiscoveryAgent` 또는 Win32 Bluetooth enumeration으로 protocol/channel/PSM 정보를 얻는다.
- 얻은 정보로 socket connect를 시도하고 handshake write/read를 확인한다.

완료 기준:

- AirPods와 socket 연결 성공.
- handshake ACK `01000400` 또는 현재 parser가 인식하는 ACK 수신.
- `REQUEST_NOTIFICATIONS` 이후 battery/metadata/control notification 수신.

막히는 경우:

- Windows 버전은 BLE battery/status 중심으로 축소.
- control 기능은 "unsupported on Windows backend"로 UI에서 비활성화.
- MagicPods 같은 기존 Windows 앱이 제공하는 범위를 참고하되, 구현은 독립적으로 진행.

### Noise control modes

가능성: AACP socket 성공 시 높음

현재 구현:

- `AirPodsPackets::NoiseControl::getPacketForMode(mode)`
- `setNoiseControlMode`에서 socket write.
- 응답은 `HEADER = 0400040009000d` 기반으로 parse.

작업:

1. `IAirPodsControlTransport::writePacket(QByteArray)` 같은 추상 인터페이스 생성.
2. Linux backend는 기존 `QBluetoothSocket` 사용.
3. Windows backend는 PoC 결과에 따른 socket 구현 사용.
4. UI는 transport capability를 보고 활성화/비활성화.

완료 기준:

- Off/ANC/Transparency/Adaptive 전환.
- AirPods stem 또는 다른 기기에서 mode 변경 시 UI 반영.

### Conversational awareness and volume lowering

가능성: 중간

두 부분으로 나뉜다.

- AirPods 기능 toggle: AACP socket 필요.
- voice detected event에 맞춰 Windows volume 조절: Windows Core Audio로 가능.

현재 Linux 구현:

- control command id `0x28`
- data header `040004004B00020001`
- event flag `0x01`이면 현재 volume 저장 후 20%로 감소.
- voice ended면 원래 volume으로 복원.

Windows 구현 후보:

- Core Audio `IAudioEndpointVolume`
- default render endpoint 조회는 `IMMDeviceEnumerator`
- AirPods active output 판별은 endpoint friendly name 또는 device id 매핑으로 구현

완료 기준:

- Conversational Awareness packet 수신 시 현재 default endpoint가 AirPods면 volume 감소/복원.

### Ear detection playback pause/resume

가능성: 중간

현재 Linux 구현:

- AirPods packet에서 ear detection parse.
- MPRIS DBus player를 찾아 `Pause`/`Play` 호출.
- AirPods가 active output일 때만 동작.

Windows 구현 후보:

- Global System Media Transport Controls Session Manager, 즉 GSMTC.
- C++/WinRT에서 `Windows.Media.Control.GlobalSystemMediaTransportControlsSessionManager`.
- playing session을 찾아 pause/play.
- 앱별 session resume tracking이 Linux의 `pausedByAppServices`와 대응된다.

리스크:

- 모든 플레이어가 GSMTC를 제대로 지원하지는 않는다.
- 권한/패키징 요구사항 확인 필요.

완료 기준:

- Spotify/Chrome/Edge/Media Player 중 최소 2개 앱에서 pause/resume 확인.
- 사용자가 선택한 정책 `PauseWhenOneRemoved`, `PauseWhenBothRemoved` 유지.

### Audio device/profile switching

가능성: 중간에서 낮음

현재 Linux 구현:

- PulseAudio card name에서 `bluez` + MAC 주소를 찾는다.
- A2DP profile 우선순위 `a2dp-sink-sbc_xq`, `a2dp-sink-sbc`, `a2dp-sink`.
- 둘 다 귀에서 빠지면 card profile을 `off`로 바꾼다.
- A2DP profile이 없으면 WirePlumber를 재시작한다.

Windows 구현 후보:

- Core Audio endpoint enumeration.
- 기본 출력 장치 변경은 공식 public API가 제한적이다. policy config COM 우회가 널리 쓰이지만 안정적/public API라고 보기 어렵다.
- AirPods 연결/해제는 Windows Bluetooth UI/OS 정책에 맡기고, LibrePods는 media pause/volume control 중심으로 가는 편이 현실적이다.

권장 범위:

- v1에서는 default output 자동 변경을 제외.
- 사용자가 이미 AirPods를 출력 장치로 쓰는 경우 volume/pause만 동작.
- 추후 optional experimental feature로 endpoint switching 추가.

### Connection monitoring

가능성: 높음

현재 Linux 구현:

- BlueZ DBus `org.freedesktop.DBus.Properties.PropertiesChanged`.
- `org.bluez.Device1.Connected` 변화를 감시.
- service UUID로 AirPods 여부 확인.

Windows 구현 후보:

- `Windows.Devices.Enumeration.DeviceWatcher`
- `BluetoothLEDevice.ConnectionStatusChanged` for BLE side
- Classic paired device 상태는 DeviceInformation watcher 또는 Bluetooth APIs로 확인.

권장:

- v1에서는 BLE advertisement seen timestamp와 AACP socket state를 조합.
- Windows device connection state는 보조 신호로 사용.

### Tray and notification

가능성: 높음

현재 구현:

- `QSystemTrayIcon`, `QMenu`, `showMessage`.

Windows:

- Qt `QSystemTrayIcon` 사용 가능.
- toast notification은 나중에 Windows App SDK 또는 WinRT로 확장 가능.

작업:

- 기존 `TrayIconManager`를 대부분 유지.
- Windows에서 tray icon 표시와 context menu 동작 확인.

### Autostart

가능성: 높음

현재 Linux:

- `~/.config/autostart/*.desktop` 작성.

Windows 후보:

- Startup folder shortcut
- Registry `HKCU\Software\Microsoft\Windows\CurrentVersion\Run`
- Task Scheduler

권장:

- v1: HKCU Run entry.
- installer가 생기면 installer option으로 이동.

### Sleep/wake

가능성: 높음

현재 Linux:

- logind DBus `PrepareForSleep`.

Windows 후보:

- Qt native event filter에서 `WM_POWERBROADCAST`
- `PBT_APMSUSPEND`, `PBT_APMRESUMEAUTOMATIC`, `PBT_APMRESUMESUSPEND`

작업:

- `ISystemSleepMonitor` 추상화.
- Windows implementation은 native event filter.

### Hearing Aid and custom transparency

가능성: 낮음

현재 Linux README 기준:

- AirPods가 Apple device 여부를 DeviceID characteristic으로 확인한다.
- Linux는 `/etc/bluetooth/main.conf`에서 `DeviceID = bluetooth:004C:0000:0000` 설정을 요구한다.
- 일부 기능은 별도 script에서 PSM/raw ATT 접근이 필요하다.

Windows 리스크:

- 일반 앱이 host Bluetooth Device ID/vendor id를 Apple로 바꾸기 어렵다.
- Raw ATT/L2CAP 접근이 제한될 수 있다.
- 드라이버 레벨 접근은 프로젝트 범위를 크게 초과한다.

권장:

- v1/v2 범위에서 제외.
- AACP control socket이 안정화된 뒤 별도 research branch에서 검증.

### Multi-device connectivity / Android relay

가능성: 낮음에서 중간

현재 Linux:

- phone MAC 환경변수 `PHONE_MAC_ADDRESS`.
- phone relay UUID `1abbb9a4-10e4-4000-a75c-8953c5471342`.
- AirPods packet을 phone socket으로 relay.
- media start 시 Android에 disconnect request.

Windows:

- AirPods AACP와 별개로 Android phone L2CAP/RFCOMM relay 채널 구현 가능성도 검증해야 한다.
- v1 범위에서는 제외 권장.

## 권장 아키텍처

현재 `linux/main.cpp`에 앱 orchestration과 Linux backend가 많이 섞여 있다. Windows 포팅 전 또는 병행해서 다음 계층으로 나누는 것이 좋다.

```text
app/
  AppController
  DeviceInfo
  Battery
  EarDetection
  AirPodsPackets
  BleAdvertisementParser
  AirPodsSession

platform/
  bluetooth/
    IAirPodsTransport
    IBleScanner
    IConnectionMonitor
  media/
    IMediaController
    IAudioEndpointController
  system/
    IAutoStartManager
    ISleepMonitor

platform/linux/
  BluezConnectionMonitor
  QtL2capAirPodsTransport
  QtBleScanner or existing BleManager
  PulseAudioEndpointController
  MprisMediaController
  LinuxAutoStartManager
  LogindSleepMonitor

platform/windows/
  WinRtBleScanner
  WindowsAirPodsTransportPoC
  WindowsConnectionMonitor
  CoreAudioEndpointController
  GsmtcMediaController
  WindowsAutoStartManager
  WindowsSleepMonitor

ui/
  qml files
  TrayIconManager
```

### 최소 인터페이스 초안

```cpp
class IBleScanner : public QObject {
    Q_OBJECT
public:
    virtual void startScan() = 0;
    virtual void stopScan() = 0;
signals:
    void deviceFound(const BleInfo& info);
    void errorOccurred(QString message);
};

class IAirPodsTransport : public QObject {
    Q_OBJECT
public:
    virtual void connectToDevice(const QString& addressOrDeviceId) = 0;
    virtual void disconnectFromDevice() = 0;
    virtual bool isConnected() const = 0;
    virtual bool writePacket(const QByteArray& packet) = 0;
signals:
    void connected();
    void disconnected();
    void packetReceived(QByteArray packet);
    void errorOccurred(QString message);
};

class IMediaController : public QObject {
    Q_OBJECT
public:
    enum MediaState { Stopped, Paused, Playing };
    virtual MediaState currentState() const = 0;
    virtual void pause() = 0;
    virtual void playPreviouslyPaused() = 0;
signals:
    void mediaStateChanged(MediaState state);
};

class IAudioEndpointController : public QObject {
    Q_OBJECT
public:
    virtual bool isAirPodsActiveOutput(const QString& airPodsId) const = 0;
    virtual int currentVolumePercent() const = 0;
    virtual bool setVolumePercent(int volume) = 0;
};
```

## Windows 기술 스택 후보

### C++/Qt 유지

권장도: 높음

장점:

- QML UI 재사용.
- 현재 C++ packet/state 코드 재사용.
- CMake 기반으로 유지 가능.

필요:

- Qt 6 for Windows: Quick, Widgets, Bluetooth는 PoC용으로만 우선 확인.
- C++20 또는 C++17.
- C++/WinRT headers.
- Windows SDK.
- OpenSSL 또는 Windows BCrypt/CNG.

### C#/.NET companion backend

권장도: 중간

장점:

- WinRT, GSMTC, CoreAudio wrapper 사용이 상대적으로 편함.
- Windows API 실험 속도가 빠름.

단점:

- Qt C++ 앱과 IPC 필요.
- 배포가 복잡해짐.
- packet/state 로직 중복 가능.

활용 방안:

- 초기 PoC만 C#으로 빠르게 검증.
- 성공한 API 호출을 C++/WinRT로 옮긴다.

### Rust backend

권장도: 낮음에서 중간

장점:

- Windows API crate와 async 구성이 좋을 수 있음.
- Linux Rust rewrite가 진행 중이면 장기 통합 가능성 있음.

단점:

- 현재 이 repo의 checked-out Linux 앱은 C++/Qt.
- 당장 포팅 속도는 C++/WinRT보다 느릴 수 있음.

## 외부 공식 문서 포인트

Microsoft 공식 문서 기준으로 Windows BLE advertisement와 GATT는 WinRT API가 제공된다.

- `Windows.Devices.Bluetooth.Advertisement` namespace는 advertisement watcher/publisher를 제공하고, manufacturer data section을 읽을 수 있다.
- `BluetoothLEAdvertisementWatcher`는 BLE advertisement scan을 시작한다.
- `Windows.Devices.Bluetooth.GenericAttributeProfile` namespace는 desktop app에서도 BLE GATT communication에 사용할 수 있다.
- Win32 Bluetooth socket 문서는 Bluetooth socket에 `AF_BTH`를 쓰며, protocol 설명은 RFCOMM 중심이다. 따라서 AACP L2CAP custom channel은 공식 문서만으로 가능하다고 단정하면 안 된다.

참고:

- https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement
- https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.advertisement.bluetoothleadvertisementwatcher
- https://learn.microsoft.com/en-us/uwp/api/windows.devices.bluetooth.genericattributeprofile
- https://learn.microsoft.com/en-us/windows/win32/bluetooth/bluetooth-and-socket
- https://learn.microsoft.com/en-us/windows/win32/bluetooth/bluetooth-and-socket-options

## 단계별 작업 계획

### Phase 0: Windows capability PoC

목표: 포팅의 성패를 가르는 API 가능성을 빠르게 판정한다.

작업:

1. Windows BLE watcher PoC 작성.
2. Apple manufacturer data `0x004C` 로그 출력.
3. 기존 AirPods advertisement parser를 분리하거나 복사해 battery/model/status 출력.
4. paired AirPods service enumeration PoC 작성.
5. UUID `74ec2172-0bad-4d01-8f77-997b2be0722a`가 보이는지 확인.
6. AACP socket connect 시도.
7. handshake packet write/read 확인.

산출물:

- `experiments/windows-ble-watcher/`
- `experiments/windows-aacp-socket/`
- PoC 결과 문서: 지원/미지원, Windows 버전, Bluetooth adapter/chipset, AirPods model/firmware.

판정:

- BLE만 성공: battery-first Windows app으로 진행.
- BLE + AACP 성공: full Windows port 진행.
- 둘 다 실패: Qt/Windows Bluetooth stack 또는 adapter 문제 조사.

### Phase 1: Core extraction

목표: Linux 코드를 깨지 않고 Windows backend를 붙일 수 있게 만든다.

작업:

1. `BleInfo`와 advertisement parser를 `core/bluetooth`로 이동.
2. `AirPodsPackets`, `Battery`, `EarDetection`, `DeviceInfo`를 platform-neutral하게 유지.
3. `AirPodsTrayApp`에서 platform-specific calls를 interface로 치환.
4. Linux backend가 기존 동작을 그대로 제공하게 래핑.
5. CMake option 추가: `LIBREPODS_PLATFORM=linux/windows` 또는 `if(WIN32)`.

주의:

- 한 번에 큰 리팩터링을 하지 말고 BLE scanner부터 interface화한다.
- Linux 앱 동작이 깨지지 않도록 기존 class 이름을 최대한 유지한다.

완료 기준:

- Linux build가 기존 기능을 유지.
- Windows target이 최소 UI/tray/core compile까지 통과.

### Phase 2: Windows BLE battery app

목표: Windows에서 사용 가능한 첫 버전을 만든다.

작업:

1. `WinRtBleScanner` 구현.
2. BLE advertisement -> `BleInfo` -> `DeviceInfo` 업데이트 연결.
3. tray tooltip/menu battery 표시.
4. QML main screen battery 표시.
5. settings 저장 확인.
6. notifications unsupported 상태 처리.

완료 기준:

- Windows에서 앱 실행.
- AirPods 케이스 열기/착용 상태에 따라 UI 업데이트.
- 종료/재실행 후 settings 유지.

### Phase 3: Windows system integration

목표: Windows 앱답게 동작하게 만든다.

작업:

1. `WindowsAutoStartManager`.
2. `WindowsSleepMonitor`.
3. tray menu/notification QA.
4. installer 또는 portable package 전략 결정.

완료 기준:

- 자동 시작 on/off.
- sleep/wake 후 BLE scan 재시작.
- tray icon 안정적으로 표시.

### Phase 4: AACP control

조건: Phase 0에서 AACP socket 성공.

작업:

1. `WindowsAirPodsTransport` 구현.
2. handshake state machine 분리.
3. packet parser를 `AirPodsSession`으로 분리.
4. noise control mode send/receive 구현.
5. conversational awareness toggle 구현.
6. one bud ANC mode 구현.
7. rename 구현은 나중으로 미룬다.

완료 기준:

- 연결 후 metadata 수신.
- noise mode 전환 및 UI 반영.
- AirPods에서 상태 변경 시 앱 반영.

### Phase 5: Media and audio

작업:

1. GSMTC 기반 play/pause 구현.
2. Core Audio volume get/set 구현.
3. conversational awareness event에 volume ducking 연결.
4. AirPods active endpoint 판별 개선.
5. endpoint switching은 experimental flag 뒤에 둔다.

완료 기준:

- 이어 감지로 playback pause/resume.
- 대화 인식 event로 volume duck/restore.

### Phase 6: Advanced features research

작업:

1. raw ATT/GATT handle 접근 가능성 확인.
2. DeviceID/vendor spoofing 가능성 조사.
3. hearing aid/custom transparency packet 검증.
4. multi-device relay PoC.

권장:

- 별도 branch에서 진행.
- v1 release blocker로 두지 않는다.

## CMake 변경 방향

현재:

```cmake
find_package(Qt6 REQUIRED COMPONENTS Quick Widgets Bluetooth DBus LinguistTools)
find_package(OpenSSL REQUIRED)
find_package(PkgConfig REQUIRED)
pkg_check_modules(PULSEAUDIO REQUIRED libpulse)
```

문제:

- Windows에는 DBus/PulseAudio/pkg-config가 기본적으로 없다.
- `Qt6::DBus`, `${PULSEAUDIO_LIBRARIES}`는 Linux 전용.

방향:

```cmake
find_package(Qt6 REQUIRED COMPONENTS Quick Widgets LinguistTools)
find_package(OpenSSL REQUIRED)

if(WIN32)
  find_package(Qt6 REQUIRED COMPONENTS Bluetooth) # optional during PoC
  target_compile_definitions(librepods PRIVATE LIBREPODS_PLATFORM_WINDOWS=1)
  target_link_libraries(librepods PRIVATE windowsapp runtimeobject)
elseif(UNIX AND NOT APPLE)
  find_package(Qt6 REQUIRED COMPONENTS Bluetooth DBus)
  find_package(PkgConfig REQUIRED)
  pkg_check_modules(PULSEAUDIO REQUIRED libpulse)
  target_compile_definitions(librepods PRIVATE LIBREPODS_PLATFORM_LINUX=1)
endif()
```

OpenSSL 대안:

- 기존 `BLEUtils`를 그대로 쓰려면 OpenSSL 유지.
- Windows 의존성을 줄이려면 BCrypt/CNG로 AES 구현 교체 가능.
- 초기 포팅은 OpenSSL 유지가 빠르다.

## 권장 브랜치/작업 단위

1. `codex/windows-port-analysis`
   - 이 문서와 PoC 계획.

2. `codex/windows-ble-poc`
   - `experiments/windows-ble-watcher`.

3. `codex/core-ble-parser`
   - parser extraction.

4. `codex/platform-abstractions`
   - interfaces and Linux adapters.

5. `codex/windows-ble-backend`
   - real Windows scanner.

6. `codex/windows-aacp-poc`
   - control channel proof.

7. `codex/windows-media-backend`
   - GSMTC/Core Audio.

## 테스트 전략

### Unit tests

추가 권장:

- BLE manufacturer payload parser fixtures.
- RPA/IRK verification fixtures.
- encrypted payload decrypt fixtures.
- AirPods control packet builder tests.
- Battery packet parser tests.

테스트 데이터:

- 실제 scan log를 hex fixture로 저장.
- privacy 문제가 있으므로 MAC/RPA는 익명화하거나 synthetic fixture 사용.

### Manual test matrix

장비:

- Windows 11 24H2 이상 권장.
- Bluetooth 5.x adapter 최소 2종이면 좋음.
- AirPods Pro 2 우선.
- 가능하면 AirPods 3/4/Max 추가.

시나리오:

- 앱 시작 전 AirPods 이미 연결됨.
- 앱 실행 후 케이스 열기.
- 왼쪽/오른쪽 한쪽만 착용.
- 양쪽 제거.
- 케이스 충전 중/비충전.
- 절전 진입/복귀.
- Bluetooth off/on.
- AirPods 재페어링.
- Spotify/Chrome/Edge media pause/resume.
- ANC/Transparency/Adaptive mode 전환.

### Logging

필수 로그:

- BLE watcher status.
- manufacturer data raw hex.
- parser result.
- AACP connection attempt/result.
- handshake packet write/read.
- media session selected target.
- Core Audio endpoint id/name.

민감 정보:

- MAC 주소, IRK, ENC key는 기본 로그에서 마스킹.
- debug mode에서만 raw 표시.

## 주요 의사결정

### 첫 Windows 릴리스 범위

권장:

- Battery/status display
- Tray menu
- Autostart
- Sleep/wake recovery
- Optional: noise control if AACP succeeds

제외:

- Hearing Aid
- VendorID spoofing
- multi-device relay
- automatic default audio endpoint switching
- root/driver 수준 기능

### UI 처리

Windows backend capability에 따라 UI를 동적으로 비활성화한다.

예:

- `capabilities.canReadBleBattery`
- `capabilities.canUseAacpControl`
- `capabilities.canControlMedia`
- `capabilities.canControlVolume`
- `capabilities.canSwitchAudioEndpoint`
- `capabilities.canUseHearingAid`

사용자에게 "Windows에서 현재 지원되지 않음"을 명확히 표시하되, 앱 첫 화면이 오류처럼 보이지 않게 한다.

## 다음 세션 시작 명령

```powershell
cd D:\Project\librepods
git status --short --branch
rg -n "QBluetoothSocket|QBluetoothDeviceDiscoveryAgent|QDBus|PulseAudio|bluetoothctl|systemctl|autostart|PrepareForSleep" linux
```

추천 첫 작업:

1. `experiments/windows-ble-watcher` 생성.
2. WinRT BLE watcher로 Apple manufacturer data dump.
3. `linux/ble/blemanager.cpp` parser를 작은 pure function으로 분리.
4. PoC에서 parser 재사용.

추천 두 번째 작업:

1. `experiments/windows-aacp-socket` 생성.
2. paired AirPods service enumeration.
3. AACP UUID discovery.
4. handshake connect/write/read.

## Go / No-Go 기준

Go:

- Windows BLE watcher에서 AirPods manufacturer data 안정 수신.
- UI/tray compile 가능.
- AACP socket이 성공하거나, battery-only MVP라도 가치가 있다고 판단.

Conditional Go:

- BLE는 성공하지만 AACP 실패.
- 이 경우 Windows v1은 battery/status app으로 명확히 축소.

No-Go:

- Windows에서 AirPods BLE advertisement를 안정적으로 수신하지 못함.
- Qt/WinRT packaging 문제가 core 기능보다 더 큰 비용을 요구함.
- 프로젝트 목표가 "Android 수준의 모든 고급 기능"이면 Windows 일반 앱으로는 범위가 맞지 않음.

