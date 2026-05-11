# WSL2 + usbipd-win을 통한 Windows 풀 기능 우회 가이드

이 문서는 Windows에서 LibrePods의 **풀 기능**(AACP 제어 — ANC 모드, 대화 모드, 헤드 제스처 등)을 사용하고 싶은 사용자를 위한 escape hatch 가이드입니다.

> ⚠️ **이 방법은 외장 USB Bluetooth 동글이 필요합니다.** 노트북 내장 어댑터는 Hyper-V USB passthrough가 불가능합니다 (PCIe로 직접 연결돼 있어서). 동글은 5천원~2만원대 CSR/Realtek 칩셋 짜리도 충분합니다.

## 왜 이게 동작하는가

[저장소 README](../README.md)와 [`PROBE-RESULTS.md`](../experiments/windows-feasibility/RESULTS.md)에 자세히 정리돼 있듯이, Windows userspace는 raw L2CAP 소켓 접근을 막아 놨습니다. **BlueZ (Linux)는 이 제약이 없고**, 원본 LibrePods Linux 클라이언트는 BlueZ의 표준 L2CAP 소켓 API를 그대로 사용합니다.

WSL2는 본질적으로 Hyper-V VM이고, **`usbipd-win`을 통해 USB 장치를 통째로 WSL2에 attach** 하면, WSL2의 BlueZ가 그 장치를 **물리 어댑터로 인식**합니다. Windows는 해당 장치를 더 이상 사용할 수 없게 되지만, AACP 제어는 BlueZ를 통해 정상적으로 동작합니다.

## 사전 요구

- Windows 10 21H2+ 또는 Windows 11
- WSL2 활성화 + Ubuntu 22.04+ (또는 Debian/Arch 등)
- 외장 USB Bluetooth dongle (CSR8510 / Realtek RTL8761 등 BlueZ 호환)
- AirPods 페어링 정보 (Windows 페어링은 풀어야 함 — 한 번에 한 호스트만 가능)

## 1. usbipd-win 설치

PowerShell (관리자):

```powershell
winget install --interactive --exact dorssel.usbipd-win
```

설치 후 시스템 재시작 권장.

## 2. WSL2 측 준비

WSL2 안에서 (Ubuntu 기준):

```bash
sudo apt update
sudo apt install -y linux-tools-virtual hwdata
sudo update-alternatives --install /usr/local/bin/usbip usbip \
  /usr/lib/linux-tools/*-generic/usbip 20

# BlueZ + Qt 의존성
sudo apt install -y bluez bluez-tools qt6-base-dev qt6-declarative-dev \
  qt6-bluetooth qt6-tools-dev libssl-dev libpulse-dev pkg-config \
  cmake ninja-build build-essential
```

## 3. 동글을 WSL2에 attach

PowerShell (관리자):

```powershell
# 연결된 USB 장치 목록 확인
usbipd list

# 출력 예시:
# BUSID  VID:PID    DEVICE
# 1-4    0a12:0001  Bluetooth Adapter (CSR)
# ...

# Bluetooth 동글의 BUSID 확인 후 (예: 1-4)
usbipd bind --busid 1-4
usbipd attach --wsl --busid 1-4
```

이 시점에서 **Windows의 Bluetooth 어댑터 목록에서 동글이 사라지고**, WSL2 안에서 보이게 됩니다.

WSL2 안에서 확인:

```bash
lsusb        # CSR/Realtek 항목이 보여야 함
hciconfig -a # hci0 (또는 hci1) 항목이 보여야 함

# Bluetooth 서비스 시작
sudo service bluetooth start
sudo hciconfig hci0 up
```

## 4. AirPods 페어링

```bash
sudo bluetoothctl
```

```
power on
agent on
default-agent
scan on
# AirPods 케이스 열고 setup 버튼 길게 (LED 흰색 깜빡)
# AirPods MAC 확인 후
pair XX:XX:XX:XX:XX:XX
trust XX:XX:XX:XX:XX:XX
connect XX:XX:XX:XX:XX:XX
```

## 5. LibrePods 빌드

이 저장소(또는 [원본](https://github.com/kavishdevar/librepods))를 clone:

```bash
git clone https://github.com/jeiel85/librepods-windows-ble.git
cd librepods-windows-ble/linux
mkdir build && cd build
cmake .. -G Ninja
ninja
./librepods
```

(`linux/` 디렉토리 코드는 원본 그대로라 풀 기능 동작합니다.)

## 6. WSL2에서 GUI 표시

WSLg(Windows 11) 또는 WSL2 + X server (Windows 10):
- **Windows 11**: WSLg 기본 내장. 별도 설정 없이 GUI 앱이 Windows 데스크톱에 표시됨.
- **Windows 10**: VcXsrv / X410 같은 X server 설치 후 `DISPLAY=:0` 같은 환경 변수 설정.

오디오는 WSLg가 PulseAudio 기본 라우팅 처리. Windows 10이면 PulseAudio over TCP 별도 설정 필요.

## 자주 발생하는 문제

### `usbipd attach` 실패

```
attach failed: WSL distribution not running or no usbip module
```

→ WSL 안에서 `sudo modprobe vhci-hcd` 실행. 모듈 없으면 `linux-modules-extra-$(uname -r)` 설치.

### `bluetoothctl pair` 실패

→ 페어링 시도 전에 Windows에서 해당 AirPods를 **반드시 unpair** (Bluetooth 설정 → 장치 제거). 한 호스트만 페어링 가능.

### 재부팅 후 동글이 다시 Windows로

`usbipd bind --busid X-Y --auto-attach` 옵션 사용하면 부팅마다 자동 attach. 또는 매번 수동:

```powershell
usbipd attach --wsl --busid X-Y
```

### AACP 안 되는데 BLE는 됨

→ BR/EDR 연결 안 된 상태. `bluetoothctl connect XX:XX:XX:XX:XX:XX` 또는 음악 재생 시도.

## 트레이드오프 정리

| 항목 | Windows BLE 트레이 (이 fork) | WSL2 + usbipd |
|------|-------------------------------|----------------|
| 추가 하드웨어 | 불필요 | USB BT 동글 필요 |
| 설정 복잡도 | exe 실행 | 위 단계들 + 페어링 재설정 |
| 능동 제어 | ❌ | ✅ |
| Windows 내장 어댑터 사용 | ✅ | ❌ (passthrough된 동글 전용) |
| AirPods를 다른 Windows 앱에서 동시 사용 | ✅ | △ (동글이 WSL2 점유) |

대부분의 사용자에게는 Windows BLE 트레이로 충분합니다. **AACP 제어가 진짜 필요한 파워유저**만 이 가이드를 따라가세요.

## 참고

- [usbipd-win 공식 문서](https://github.com/dorssel/usbipd-win/wiki)
- [WSL USB device support 공식 가이드](https://learn.microsoft.com/en-us/windows/wsl/connect-usb)
- [원본 LibrePods Linux 가이드](https://github.com/kavishdevar/librepods/blob/main/linux/README.md)
