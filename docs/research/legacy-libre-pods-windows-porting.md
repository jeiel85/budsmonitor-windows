# Legacy LibrePods Windows Porting Notes

This repository used to be `jeiel85/librepods-windows-ble`, a Windows BLE-only fork of LibrePods.

The reusable parts for BudsMonitor are:

- the C# AirPods advertisement parser proof of concept in `experiments/windows-feasibility/battery-tray-mvp/Program.cs`
- the Windows BLE scanner behavior captured by the feasibility probes
- the C++ BLE parser and state model under `linux/ble/`
- the feasibility evidence showing why AACP active control is not a v1.0 Windows userspace target

The Linux/Qt layout remains as historical evidence. New production work should happen in the C#/.NET solution under `src/`.
