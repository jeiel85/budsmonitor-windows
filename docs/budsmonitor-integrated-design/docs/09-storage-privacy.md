# 09. Storage and Privacy

## Policy

BudsMonitor is local-only.

Hard rules:

```text
- No account
- No login
- No analytics
- No ads
- No telemetry
- No crash upload
- No network calls in normal operation
```

## Storage locations

```text
%AppData%\BudsMonitor\settings.json
%AppData%\BudsMonitor\devices.json
%LocalAppData%\BudsMonitor\cache\battery-cache.json
%LocalAppData%\BudsMonitor\logs\app-yyyyMMdd.log
%LocalAppData%\BudsMonitor\diagnostics\
```

## Settings schema

```json
{
  "version": 1,
  "app": {
    "startWithWindows": true,
    "minimizeToTray": true,
    "theme": "system",
    "language": "ko-KR"
  },
  "monitoring": {
    "enableAirPodsBleScanner": true,
    "enableGenericGattRefresh": true,
    "genericGattPollingIntervalSeconds": 300,
    "staleAfterSeconds": 120,
    "lastKnownVisibleForHours": 24
  },
  "notifications": {
    "enabled": true,
    "earbudLowBatteryThreshold": 20,
    "caseLowBatteryThreshold": 20,
    "suppressRepeatedMinutes": 60,
    "notifyFromStaleData": false,
    "quietHoursEnabled": false,
    "quietHoursStart": "22:00",
    "quietHoursEnd": "07:00"
  },
  "privacy": {
    "maskBluetoothAddressesInLogs": true,
    "includeRawPayloadsInDiagnostics": false,
    "analyticsEnabled": false,
    "networkEnabled": false
  }
}
```

## Device registry schema

```json
{
  "version": 1,
  "devices": [
    {
      "stableDeviceKey": "sha256:...",
      "displayName": "AirPods Pro 2",
      "alias": "My AirPods",
      "providerHint": "airpods-ble-advertisement",
      "isPinned": true,
      "isHidden": false,
      "firstSeenAt": "2026-07-08T13:42:00+09:00",
      "lastSeenAt": "2026-07-08T13:42:00+09:00"
    }
  ]
}
```

## Battery cache schema

```json
{
  "version": 1,
  "snapshots": [
    {
      "stableDeviceKey": "sha256:...",
      "displayName": "AirPods Pro 2",
      "source": "AirPodsBleAdvertisement",
      "measuredAt": "2026-07-08T13:42:00+09:00",
      "expectedFreshnessSeconds": 30,
      "components": [
        { "type": "LeftBud", "percentage": 80, "isCharging": false },
        { "type": "RightBud", "percentage": 90, "isCharging": false },
        { "type": "Case", "percentage": 60, "isCharging": true }
      ]
    }
  ]
}
```

## Bluetooth address handling

- In memory: raw address may be used when required by Windows API.
- In settings/cache/logs: store hash by default.
- In diagnostics: mask by default, optionally include raw only when user explicitly chooses advanced mode.

Masking example:

```text
C4:0B:31:**:**:**
```

Hashing example:

```text
sha256:0f4c...ab91
```

Use a local salt stored in settings to avoid stable cross-machine tracking.

## Log retention

Default:

```text
- Keep app logs for 14 days.
- Keep diagnostic bundles until user deletes them.
- Rotate log files daily or at 10 MB.
```

## No-network verification

Add a release checklist item:

```text
Search codebase for HttpClient, WebClient, Socket, DNS, analytics SDKs, telemetry SDKs.
Verify none are used in normal app code.
```
