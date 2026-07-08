# Release Checklist

## Build

- [ ] Clean restore succeeds.
- [ ] Release build succeeds.
- [ ] All tests pass.
- [ ] Portable ZIP generated.
- [ ] App runs from extracted folder.

## Functionality

- [ ] Tray icon appears.
- [ ] Dashboard opens from tray.
- [ ] Quit fully exits.
- [ ] AirPods provider tested with real device.
- [ ] Generic GATT provider tested with real Battery Service device.
- [ ] Low battery notification tested.
- [ ] Settings persist.
- [ ] Battery cache persists.
- [ ] Diagnostics ZIP generated.

## Stability

- [ ] Bluetooth off/on test passed.
- [ ] Sleep/resume test passed.
- [ ] 8-hour run test passed.
- [ ] Scanner restart tested.
- [ ] No tight retry loop found.
- [ ] Logs rotate or remain bounded.

## Privacy

- [ ] No account/login.
- [ ] No analytics.
- [ ] No ads.
- [ ] No telemetry.
- [ ] No network calls in normal code.
- [ ] Bluetooth addresses masked in diagnostics by default.

## Documentation

- [ ] README updated.
- [ ] Known limitations documented.
- [ ] AirPods active controls explicitly out of scope.
- [ ] Troubleshooting guide included.
- [ ] License included.
