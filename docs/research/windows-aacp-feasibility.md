# Windows AACP Feasibility Summary

The legacy feasibility probes found that normal Windows userspace can receive AirPods BLE proximity advertisements, but cannot open the AirPods active-control channel through raw Classic Bluetooth L2CAP.

Important source documents:

- `experiments/windows-feasibility/RESULTS.md`
- `docs/windows-porting-progress.md`
- `docs/wsl2-usbipd-guide.md`

Product decision:

- BudsMonitor v1.0 is BLE-only for AirPods.
- ANC, transparency, conversation awareness, firmware management, and other AACP active controls remain out of scope.
- WSL2 USB dongle passthrough remains a research/power-user path, not a normal app dependency.
