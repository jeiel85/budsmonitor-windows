# ADR-001: Build a new WPF app instead of continuing the Qt MVP

## Status

Accepted

## Context

The previous repository already has a Qt/QML tray MVP and C++/WinRT scanner. It proves the AirPods BLE path but is structured around a Linux LibrePods fork.

The desired product is a Windows daily-driver utility with settings, cache, notifications, diagnostics, and long-term maintainability in the user's Windows development workflow.

## Decision

Create a new C#/.NET/WPF app and port/reference the AirPods BLE parser and scanner behavior.

## Consequences

Positive:

- Easier Windows desktop maintenance.
- Easier settings/logging/diagnostics integration.
- Easier future packaging as Windows utility.
- Existing C# feasibility parser can be reused.

Negative:

- Qt tray MVP UI code is not directly reused.
- Some C++ parser behavior must be verified during port.
