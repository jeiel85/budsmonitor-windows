# Codex Prompt — GOAL 3 AirPods Parser Port

You are implementing GOAL 3 for BudsMonitor for Windows.

## Context

We are building a C# WPF Windows tray app. The previous repository `jeiel85/librepods-windows-ble` contains a C# feasibility AirPods advertisement parser in `experiments/windows-feasibility/battery-tray-mvp/Program.cs`. Port the parser into the new provider project without bringing over UI code.

## Tasks

1. Create project `BudsMonitor.Providers.AirPods` if it does not exist.
2. Add:
   - `AirPodsAdvertisementParser.cs`
   - `AirPodsAdvertisementSnapshot.cs`
   - `AirPodsModelCatalog.cs`
   - `AirPodsBatteryMapper.cs`
3. Parser API:

```csharp
public static bool TryParse(
    ReadOnlySpan<byte> data,
    AirPodsAdvertisementContext context,
    out AirPodsAdvertisementSnapshot snapshot)
```

4. Map battery nibble `15` to `null`; map `0..10` to `0..100` in 10% increments.
5. Do not throw on malformed payloads.
6. Add xUnit tests for valid, invalid, too-short, unknown model, unavailable battery, and primary-left flip behavior.

## Constraints

- No network code.
- No UI code in provider project.
- Keep parser deterministic and side-effect free.
- Any copied/adapted code must retain GPL-compatible licensing notice if applicable.

## Done when

- Unit tests pass.
- Parser can be called by a scanner event pipeline.
- No malformed payload causes an exception.
