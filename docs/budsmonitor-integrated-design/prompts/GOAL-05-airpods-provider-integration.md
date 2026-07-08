# Codex Prompt — GOAL 5 AirPods Provider Integration

Integrate AirPods BLE advertisement parsing into the app state.

## Tasks

1. Create `AirPodsBleAdvertisementProvider` implementing `IAdvertisementBatteryProvider`.
2. Parse only Company ID `0x004C` frames.
3. Convert successful parse result into `BatterySnapshot`.
4. Save snapshot to `BatteryCacheRepository`.
5. Update device card state through application service, not directly from scanner callback.
6. Update tray tooltip and menu summary.
7. Implement stale thresholds:
   - 0–30s live
   - 30–120s recently seen
   - 2–15m stale
   - 15m+ last-known only

## Acceptance

- Real AirPods advertisements update UI.
- Missing component values are shown as unavailable, not 0%.
- Stale values are visually marked.
- App remains responsive when no packets arrive.
