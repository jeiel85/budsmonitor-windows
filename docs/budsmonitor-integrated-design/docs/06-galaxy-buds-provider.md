# 06. Galaxy Buds Provider

## Status

Galaxy Buds support is part of the product vision, but it is not as ready as AirPods support because the existing repository only validates AirPods BLE advertisements.

Treat Galaxy Buds as a provider track with a production gate.

## v1.0 target

At minimum:

- Detect Galaxy Buds-like device names.
- Show generic Battery Service data if available.
- Keep a clear unsupported/diagnostic state if model-specific data is not yet decoded.
- Provide diagnostics to capture GATT services and BLE advertisements.

Stretch target:

- Left/right/case battery through Samsung-specific BLE/GATT behavior if discoverable.

## Provider placement

```text
src/BudsMonitor.Providers.GalaxyBuds/
  GalaxyBudsProvider.cs
  GalaxyBudsDeviceClassifier.cs
  GalaxyBudsProfileCatalog.cs
  GalaxyBudsDiagnosticsCollector.cs
```

## Model profiles

```text
profiles/galaxy-buds.json
profiles/galaxy-buds-plus.json
profiles/galaxy-buds-live.json
profiles/galaxy-buds-pro.json
profiles/galaxy-buds2.json
profiles/galaxy-buds2-pro.json
profiles/galaxy-buds3.json
profiles/galaxy-buds3-pro.json
```

Profile schema:

```json
{
  "profileId": "samsung.galaxy-buds2-pro",
  "displayName": "Galaxy Buds2 Pro",
  "namePatterns": ["Galaxy Buds2 Pro", "Buds2 Pro"],
  "preferredProviders": [
    "galaxy-buds-provider",
    "standard-gatt-battery"
  ],
  "diagnosticHints": {
    "captureAdvertisements": true,
    "captureGattServices": true
  }
}
```

## Initial detection logic

```csharp
public bool IsGalaxyBudsCandidate(DeviceCandidate device)
{
    var name = device.DisplayName;
    return name.Contains("Galaxy Buds", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Buds2", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Buds3", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Buds Pro", StringComparison.OrdinalIgnoreCase)
        || name.Contains("Buds Live", StringComparison.OrdinalIgnoreCase);
}
```

## Implementation phases

### Phase 1 — Classification and fallback

- Detect Galaxy Buds by name/profile.
- Try `StandardGattBatteryProvider`.
- If standard GATT has only whole-device battery, show it honestly.
- If no battery exists, show diagnostic prompt.

### Phase 2 — Diagnostics capture

Add one-click capture for selected Galaxy Buds:

```text
- BLE advertisements for 60 seconds
- Manufacturer data sections
- Service UUID list
- Characteristic UUID list
- Battery Service presence
- Read attempts and errors
```

### Phase 3 — Protocol decoding

Only after real diagnostics exist:

- Identify Samsung manufacturer data pattern if present.
- Identify custom GATT service if present.
- Implement parser with tests.
- Add model-specific profile.

### Phase 4 — Production gate

Do not mark a Galaxy Buds model as supported until:

- At least one real device capture exists.
- Parser has unit tests with sample payloads.
- UI shows left/right/case or clearly states fallback limits.
- Failure states are mapped.

## UI behavior

If Galaxy Buds detailed data is not yet supported:

```text
Galaxy Buds2 Pro
Connected · Limited support

Battery: 70%  // from standard GATT if available

Detailed left/right/case data is not available yet.
[Generate diagnostics]
```

This is better than pretending full support.
