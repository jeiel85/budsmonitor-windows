# AirPods "연결" 버튼 — 설계 계획서 (검토용)

상태: **제안 / 미승인**. 작성 2026-07-09. 구현 전 검토·확정용 문서.
관련: [windows-aacp-feasibility.md](windows-aacp-feasibility.md), `docs/budsmonitor-integrated-design/adr/ADR-002-ble-only-airpods-v1.md`, `docs/TROUBLESHOOTING.md`.

---

## 1. 목표와 범위

MagicPods처럼, 페어링된 AirPods를 **Windows 오디오 장치로 연결/해제**하는 버튼을 기기 카드에 추가한다.

- **v1 (이 계획의 확실한 산출물): 수동 "연결"/"연결 해제" 버튼.** 사용자가 누르면 해당 AirPods를 오디오로 붙이거나 뗀다. 현재 연결 상태를 카드에 표시.
- **v2 (실험적, 별도·후속): "연결 유지"(자동 재연결) opt-in 토글.** 신뢰성 약속 없이 실험 기능으로만. 수동 연결이 실기기에서 확실히 동작한 뒤에만 착수.

**비목표**: ANC/투명도·제스처·이름변경 등 AACP 제어(ADR-002대로 범위 밖, 유저스페이스 불가). 이 계획은 오직 "오디오 연결"만 다룬다.

---

## 2. 배경 — 기존 "불가" 결론의 정정

저장소는 `experiments/windows-feasibility/RESULTS.md`에서 유저스페이스 능동 연결이 **불가**라고 결론지었다. 재조사 결과 그 결론은 **AACP 제어 채널(raw L2CAP)** 에 대한 것이고, "오디오 장치로 연결"은 **시도된 적 없는 별개의 경로**가 있다.

| 접근 | 결과 | 비고 |
|---|---|---|
| raw L2CAP (AACP) | WSAENETDOWN | 제어 채널 — 범위 밖 |
| `BluetoothSetServiceState`(A2DP/HFP) | 87 INVALID_PARAMETER | **이 API는 드라이버 설치/제거용**, 런타임 연결 아님 (MS 문서 확인) |
| 관리자 HCI | 1314 | 범위 밖 |
| **Core Audio + Kernel Streaming** | **미시도** | ← 본 계획이 쓰는 경로 |

근거: MS 공식 드라이버 문서(`KSPROPSETID_BtAudio`, HFP device connection) + MagicPods류가 실제 쓰는 오픈소스 3종(ToothTray/ToothTrayCli/bluetooth_audio_switch). **무관리자·무커널드라이버.**

---

## 3. 기술 접근 (Core Audio + Kernel Streaming)

연결/해제는 Windows 오디오 스택을 통해 이뤄진다. 흐름:

1. **엔드포인트 열거** — `IMMDeviceEnumerator::EnumAudioEndpoints(eAll, DEVICE_STATEMASK_ALL)`.
   `DEVICE_STATE_UNPLUGGED`까지 포함해야 "연결 해제된(회색) AirPods"가 보인다.
2. **AirPods 엔드포인트 식별** — 엔드포인트 friendly name + **`PKEY_Device_ContainerId`** 로 묶기. 이름은 앱이 이미 읽는 `PairedBleDeviceEnumerator.GetPairedClassicNamesAsync()`("용은의 AirPods" 등)와 상관지어 "내 것"만 고른다. (ContainerId는 안정 식별자 → 회전 주소 문제 회피, [[airpods-no-stable-id]] 무관.)
3. **KS 필터의 `IKsControl` 획득** — `IMMDevice::Activate(IID_IDeviceTopology)` → Device Topology를 따라 엔드포인트에 직결된 KS 필터의 `IMMDevice` → `Activate(IID_IKsControl)`. (MS "Using the IKsControl Interface" 패턴.)
4. **연결/해제 명령** — `IKsControl::KsProperty`로
   - 연결: `KSPROPSETID_BtAudio` / `KSPROPERTY_ONESHOT_RECONNECT`
   - 해제: `KSPROPSETID_BtAudio` / `KSPROPERTY_ONESHOT_DISCONNECT`
   드라이버가 HFP 드라이버에 `REQUESTCONNECT`/`REQUESTDISCONNECT` IOCTL 전달(문서 명시).
5. **상태 판정** — 엔드포인트 `DEVICE_STATE` (`ACTIVE`=연결 / `UNPLUGGED`=해제). 폴링 또는 `IMMNotificationClient` 콜백.

**필요 입력**: BT MAC이 아니라 **오디오 엔드포인트**(이름/ContainerId). **전제조건**: 페어링 + 최소 1회 연결되어 엔드포인트·KS 필터가 생성돼 있어야 함.

---

## 4. 타당성 게이트 (구현 착수 전 필수 확인)

**미지수 1건**: "연결 해제된 AirPods가 `DEVICE_STATE_UNPLUGGED` 엔드포인트로 실제로 잡히는가?"
- RESULTS.md의 "사운드 패널에 회색 AirPods" 관찰이 바로 그 UNPLUGGED 엔드포인트일 가능성이 큼(=RECONNECT 대상). 당시 "엔드포인트 미등록" 해석이 틀렸을 개연성.
- **프로토타입 #1** (~30줄 C# 콘솔): `EnumAudioEndpoints(eAll, DEVICE_STATEMASK_ALL)`로 name/state/ContainerId 출력. AirPods를 **연결 해제**한 상태에서 실행.
  - ✅ UNPLUGGED 엔드포인트로 잡히면 → 연결 경로 성립, 본 계획 진행.
  - ❌ 아예 안 잡히면 → 이 방식 불가, 방향 재검토(계획 폐기 또는 반자동 대안).

> **결정 규칙: 프로토타입 #1이 실패하면 이 기능 전체를 보류한다.** 코드/UI를 먼저 만들지 않는다.

---

## 5. 아키텍처 배치

**제안: 신규 클래스 라이브러리 `BudsMonitor.Audio`** (`net10.0-windows10.0.17763.0`).
- Core Audio COM interop(마샬링 무거움)을 한곳에 격리. Domain(순수)·Bluetooth(WinRT)와 분리.
- 공개 계약(Domain에 인터페이스, 구현은 Audio):
  ```csharp
  // Domain
  public enum AudioLinkState { Connected, Disconnected, Unknown }
  public sealed record AudioEndpointRef(string Id, string FriendlyName, string ContainerId, AudioLinkState State);
  public interface IAudioDeviceConnector {
      IReadOnlyList<AudioEndpointRef> Enumerate();          // UNPLUGGED 포함
      AudioLinkState GetState(string endpointId);
      bool Connect(string endpointId);                       // ONESHOT_RECONNECT
      bool Disconnect(string endpointId);                    // ONESHOT_DISCONNECT
  }
  ```
- **대안**: 별도 프로젝트 없이 `BudsMonitor.Bluetooth`에 추가(오디오도 BT라). 격리 이점이 줄어 비권장.
- **의존성 고려**: MMDevice/DeviceTopology 열거는 NAudio/CSCore 래퍼로 줄일 수 있으나, `IKsControl`+`KSPROPERTY_ONESHOT_*`는 어느 라이브러리에도 없어 **직접 interop 필수**. 오프라인·자기완결 방침상 **외부 의존성 없이 직접 interop** 권장(참조: ToothTray C++ 그대로 이식).

---

## 6. 데이터 흐름 / 매칭

```
GetPairedClassicNamesAsync() ─┐   (내 AirPods 이름들)
                              ├─→ 이름/ContainerId 상관 → "내 오디오 엔드포인트" 집합
IAudioDeviceConnector.Enumerate() ─┘
        │
        ├─ 카드(DeviceListResolver의 family 카드)에 연결 상태 배지 + [연결]/[해제] 버튼
        └─ 버튼 클릭 → Connect/Disconnect(endpointId) → 상태 갱신
```

- 카드↔엔드포인트 매핑: 계열 카드(예: `family:airpods-pro`)를 해당 엔드포인트에 연결. 이름 매칭 실패 시 버튼 비활성 + 툴팁("오디오 엔드포인트 없음 — 한 번 수동 연결 후 사용 가능").

---

## 7. UI 통합

- **기기 카드**(`MainWindow.xaml` + `DeviceCardViewModel`): 우측에 **[연결]**(미연결 시) / **[해제]**(연결 시) 버튼 + 상태 배지. `App`에 `OnToggleConnect(key)` 핸들러 → 백그라운드 스레드에서 Connect/Disconnect 실행(연결은 수 초 걸릴 수 있어 UI 블로킹 금지, 진행 표시).
- 엔드포인트 없음/전제 미충족 시 버튼 숨김 또는 비활성.
- 테마·비모달 규칙은 기존 패턴 준수.

---

## 8. "연결 유지" (v2, 실험적)

- 메커니즘: `IMMNotificationClient` 또는 폴링으로 UNPLUGGED 감지 → `ONESHOT_RECONNECT` 재발행 루프(디바운스).
- **리스크**(반드시 UI에 고지): AirPods 착용감지 전력관리와 충돌 / 폰과의 연결 핑퐁 / HFP 유지 시 SCO 저음질·배터리 소모. Windows BT 오디오 재연결 자체가 불안정.
- **정책**: Settings에 "연결 유지(실험적)" opt-in 토글, 기본 off, 명시적 경고 문구. 헤드라인 기능으로 신뢰성 약속 금지. **수동 연결(v1)이 실기기에서 확실히 동작한 뒤에만 착수.**

---

## 9. 단계 / 마일스톤

| 단계 | 산출물 | 게이트 |
|---|---|---|
| P1 | 프로토타입 #1 — 엔드포인트 열거 콘솔 | UNPLUGGED AirPods 엔드포인트 확인 |
| P2 | 프로토타입 #2 — IKsControl reconnect 콘솔(ToothTray 이식) | 실제로 AirPods 온라인 전환 |
| P3 | `BudsMonitor.Audio` + `IAudioDeviceConnector` + 단위테스트(순수 파트) | 빌드/테스트 |
| P4 | 카드 [연결]/[해제] 버튼 + 상태 배지 + App 배선 | 실기기 수동 연결 실증 |
| P5 (후속) | "연결 유지" 실험 토글 | 실기기 안정성·배터리 실측 |

각 단계는 이전 게이트 통과 후에만 진행. P1/P2는 버릴 수 있는 프로토타입.

---

## 10. 테스트 전략

- **순수 로직**(이름↔엔드포인트 매칭, 상태 매핑)은 인터페이스 뒤로 빼서 단위 테스트.
- COM interop·실제 연결은 **실기기 수동 검증**(프로토타입 콘솔 + 앱 실행)으로. 자동화 어려움 명시.
- 회귀: 엔드포인트 없음/이름 매칭 실패/이미 연결됨 등 경계 조건.

---

## 11. 리스크 & 완화

| 리스크 | 완화 |
|---|---|
| UNPLUGGED 엔드포인트 미등록(P1 실패) | **게이트에서 조기 중단** — 코드 전에 확인 |
| COM interop 마샬링 버그 | ToothTray 레퍼런스 그대로 이식, 작은 콘솔로 선검증 |
| 연결이 수 초 소요/실패 | 비동기 + 타임아웃 + 사용자 피드백 |
| 자동 재연결 부작용 | v2로 분리, opt-in·경고·디바운스 |
| 다중 프로파일(A2DP+HFP) 중복 엔드포인트 | ContainerId로 묶어 대표 하나로 |

---

## 12. 문서 갱신 (하드닝 후보)

구현 시 다음을 "AACP 제어(차단) vs 오디오 연결(가능)"로 구분해 갱신:
- `ADR-002-ble-only-airpods-v1.md` — 오디오 연결은 이 ADR 범위 밖임을 명시.
- `docs/TROUBLESHOOTING.md` — "auto-reconnect 미구현"을 "수동 연결 제공, 자동 재연결은 실험적" 으로.

---

## 13. 사용자 결정 필요 항목

1. **아키텍처**: 신규 `BudsMonitor.Audio` 프로젝트(권장) vs `BudsMonitor.Bluetooth`에 추가?
2. **의존성**: 직접 interop(권장) vs NAudio/CSCore로 열거 파트 축약?
3. **범위**: v1(수동 연결)만 먼저? "연결 유지"(v2)는 별도 승인?
4. **시작점**: 승인 시 P1(프로토타입 #1)부터. AirPods를 PC에서 **연결 해제**해두면 검증이 깨끗함.
