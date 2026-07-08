# BudsMonitor for Windows — Integrated Production Design Bundle

This bundle defines a production-grade Windows tray app for daily Bluetooth earbuds monitoring.

It integrates the previous `jeiel85/librepods-windows-ble` work as the AirPods BLE provider foundation, while expanding the product into a broader daily-driver app for AirPods, Galaxy Buds, and generic BLE Battery Service devices.

## Primary goal

Build a Windows app that the owner can keep running every day and rely on for:

- AirPods / AirPods Pro battery status from BLE proximity advertisements
- Left / right / case battery where available
- In-ear status for AirPods where available
- Galaxy Buds support through a separate provider track
- Generic BLE Battery Service fallback for other devices
- Tray-first UX, cache, stale-state handling, notifications, diagnostics, and privacy-first local-only operation

## Explicit non-goals for v1.0

The existing feasibility work shows that normal Windows userspace is not a viable path for AirPods AACP active control through raw Classic Bluetooth L2CAP. Therefore v1.0 excludes:

- ANC mode switching
- Transparency / conversation mode switching
- Head gestures configuration
- Firmware updates
- AirPods rename / deep configuration
- Custom Windows kernel driver
- WSL2 USB Bluetooth dongle bridge as a default requirement

## Recommended product name

`BudsMonitor for Windows`

## Repository identity

Target repository:

```text
jeiel85/budsmonitor-windows
```

The current `jeiel85/librepods-windows-ble` fork is the legacy research source and migration seed. New production work, solution names, package names, release artifacts, and user-facing documentation should use `BudsMonitor` / `budsmonitor-windows` naming from this point forward.

## Bundle contents

| File | Purpose |
|---|---|
| `docs/00-executive-summary.md` | Final direction and scope decision |
| `docs/01-existing-repo-integration-audit.md` | Audit of `librepods-windows-ble` reusable assets |
| `docs/02-product-requirements.md` | Daily-driver product requirements |
| `docs/03-architecture.md` | Production architecture and module boundaries |
| `docs/04-provider-model.md` | Provider contracts and resolution strategy |
| `docs/05-airpods-provider.md` | AirPods BLE advertisement integration plan |
| `docs/06-galaxy-buds-provider.md` | Galaxy Buds provider research/implementation track |
| `docs/07-generic-gatt-provider.md` | Standard BLE Battery Service fallback |
| `docs/08-ui-ux-spec.md` | Tray, main window, device card, settings UX |
| `docs/09-storage-privacy.md` | Local storage, cache, privacy, no-network policy |
| `docs/10-diagnostics.md` | Diagnostics and self-repair design |
| `docs/11-build-release.md` | Build, packaging, CI, release plan |
| `docs/12-quality-test-plan.md` | Test plan and acceptance gates |
| `docs/13-implementation-goals.md` | Sequential goals sized for coding agents |
| `docs/14-risk-register.md` | Technical/legal/product risks and mitigations |
| `adr/` | Architectural decision records |
| `specs/` | JSON schemas and interface sketches |
| `prompts/` | Codex-ready implementation prompts |
| `checklists/` | Release and QA checklists |
| `SOURCES.md` | Evidence trail from the existing repository review |

## Suggested implementation strategy

1. Preserve the old repository as research evidence and AirPods BLE protocol source.
2. Start a new C#/.NET/WPF application shell.
3. Port the C# AirPods advertisement parser first.
4. Add product-grade tray UX, cache, settings, notifications, diagnostics.
5. Add generic BLE GATT Battery Service fallback.
6. Add Galaxy Buds provider after device-specific diagnostics.

## License recommendation

Because the existing LibrePods fork is GPLv3-or-later, the cleanest path is to release BudsMonitor as GPLv3-or-later if code is copied or adapted directly. If a closed-source app is desired later, do not copy GPL code; re-derive the implementation from public protocol facts and independent testing.
