# Codex Prompt — GOAL 9 Diagnostics Export

Implement local diagnostics export.

## Tasks

1. Add `DiagnosticsExportService`.
2. Export ZIP to `%LocalAppData%\BudsMonitor\diagnostics`.
3. Include:
   - `environment.json`
   - redacted settings
   - redacted device registry
   - provider results
   - recent logs
   - optional advertisement capture summary
   - optional GATT service summary
4. Mask Bluetooth addresses by default.
5. Do not upload or transmit anything.

## Acceptance

- User can generate a ZIP from Diagnostics window.
- ZIP contains useful debug data.
- Raw Bluetooth addresses are not included by default.
- Export works without admin rights.
