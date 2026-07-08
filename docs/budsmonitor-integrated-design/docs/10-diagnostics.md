# 10. Diagnostics and Self-Repair

## Purpose

Bluetooth behavior is inconsistent across Windows versions, adapters, and earbuds models. Diagnostics are not optional; they are part of product quality.

## User-facing diagnostics button

Every unsupported or failing device card should offer:

```text
[Generate diagnostics]
```

## Diagnostic export structure

```text
budsmonitor-diagnostics-YYYYMMDD-HHMMSS.zip
  environment.json
  app-settings-redacted.json
  device-registry-redacted.json
  provider-results.json
  advertisements.jsonl
  gatt-services.json
  gatt-characteristics.json
  battery-cache.json
  logs/
    app.log
    bluetooth.log
```

## Environment data

```json
{
  "appVersion": "1.0.0",
  "osVersion": "Windows 11 ...",
  "dotnetVersion": "...",
  "processArchitecture": "x64",
  "bluetoothRadioPresent": true,
  "bluetoothRadioState": "On"
}
```

## Provider results

```json
{
  "device": {
    "displayName": "Galaxy Buds2 Pro",
    "stableDeviceKey": "sha256:...",
    "addressMasked": "C4:0B:31:**:**:**"
  },
  "attempts": [
    {
      "providerId": "airpods-ble-advertisement",
      "status": "NotApplicable",
      "reason": "ProviderProtocolMismatch"
    },
    {
      "providerId": "standard-gatt-battery",
      "status": "Failed",
      "reason": "BatteryServiceNotFound"
    }
  ]
}
```

## Advertisement capture

For BLE advertisements, collect only while diagnostics capture is active.

Default capture duration:

```text
60 seconds
```

Default privacy:

- Manufacturer payload may be included only when user enables advanced diagnostics.
- Otherwise include payload length, company id, parser match result, and hash.

```json
{
  "receivedAt": "...",
  "companyId": "0x004C",
  "rssi": -62,
  "localName": "AirPods",
  "payloadLength": 27,
  "payloadHash": "sha256:...",
  "parser": "airpods-ble-advertisement",
  "parseStatus": "Success"
}
```

## GATT capture

For selected device:

```json
{
  "services": [
    {
      "uuid": "0000180f-0000-1000-8000-00805f9b34fb",
      "characteristics": [
        {
          "uuid": "00002a19-0000-1000-8000-00805f9b34fb",
          "properties": ["Read", "Notify"],
          "readStatus": "Success",
          "valueLength": 1
        }
      ]
    }
  ]
}
```

## Self-repair actions

| Problem | Automatic action |
|---|---|
| BLE scanner stopped | Restart watcher after backoff |
| Bluetooth off | Stop polling, show Bluetooth off state |
| Bluetooth returns after off | Restart scanner and refresh devices |
| Windows resumes from sleep | Delay 5 seconds, restart scanner, refresh pinned devices |
| GATT read timeout | Cancel read, mark provider failure, avoid tight retry |
| Repeated provider failure | Backoff per device |

## Backoff policy

```text
1st failure: retry after 3 seconds
2nd failure: retry after 30 seconds
3rd+ failure: retry after 5 minutes or user refresh
```

AirPods BLE scanner is continuous, but parsing failures should not log repeatedly at high volume.

## Legacy probes

The previous repo's native probes are useful for research but should not be normal app features.

Recommended location:

```text
tools/legacy-probes/
  windows_aacp_probe.cpp
  windows_hci_ioctl_probe.cpp
  windows_bredr_l2cap_probe.cpp
```

Ship them only in developer builds or advanced diagnostic packages.
