# ADR-002: AirPods v1 support is BLE-only

## Status

Accepted

## Context

The previous repository tested standard userspace paths for AACP control on Windows and found them blocked: WinRT high-level APIs do not expose AACP, L2CAP socket bind/connect fails, and admin HCI IOCTL access is not viable for normal apps.

## Decision

BudsMonitor v1.0 supports AirPods battery/status through BLE proximity advertisements only.

## Consequences

Positive:

- Product can be built without kernel drivers or extra hardware.
- Battery and in-ear status are feasible today.
- Development stays within normal Windows app permissions.

Negative:

- No ANC/transparency control.
- Some battery data may be coarse or intermittent.
