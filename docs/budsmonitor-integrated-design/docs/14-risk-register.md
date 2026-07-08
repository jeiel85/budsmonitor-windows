# 14. Risk Register

## R-001: AirPods active control impossible in normal Windows userspace

Severity: High

Status: Confirmed by existing repo feasibility work.

Mitigation:

- Exclude active control from v1.0.
- Keep old evidence docs.
- Do not spend implementation time retrying L2CAP userspace path.

## R-002: AirPods BLE battery may be coarse or intermittently absent

Severity: Medium

Mitigation:

- Show last-known values.
- Mark stale values.
- Show unavailable components individually.
- Do not overpromise 1% precision.

## R-003: BLE advertisement scanner may stop or behave differently after sleep

Severity: Medium

Mitigation:

- Add scanner stopped handler.
- Restart on resume.
- Add manual restart scan action.
- Add self-repair backoff.

## R-004: Multiple AirPods devices may be hard to distinguish

Severity: Medium

Mitigation:

- Use model/name/address hash/recent fingerprint.
- Let user alias and pin devices.
- Detect conflicts and ask for manual confirmation.

## R-005: Galaxy Buds detailed battery may not be available through standard API

Severity: Medium

Mitigation:

- Treat Galaxy Buds as limited support until diagnostics confirm protocol.
- Add GATT fallback.
- Add diagnostic capture.

## R-006: GPL licensing affects distribution model

Severity: Medium

Mitigation:

- Use GPLv3-or-later if copying/adapting existing LibrePods code.
- For closed-source future, rewrite independently without copying GPL code.

## R-007: Windows API version compatibility

Severity: Low-Medium

Mitigation:

- Target Windows 10 1809+ initially.
- Runtime feature-check Bluetooth APIs.
- Keep Windows 11 as primary test target.

## R-008: App drains battery or CPU due to scanning

Severity: Medium

Mitigation:

- Measure idle CPU.
- Avoid excessive logging.
- Use backoff and channel-based processing.
- Consider scanner pause when no devices are relevant, if needed.

## R-009: Diagnostics leak private identifiers

Severity: Medium

Mitigation:

- Mask Bluetooth addresses by default.
- Hash stable keys with local salt.
- Advanced raw payload export requires explicit user action.

## R-010: User expectations exceed BLE-only scope

Severity: Medium

Mitigation:

- Product copy must say battery/status monitor.
- Do not display controls that cannot work.
- Document why active controls are excluded.
