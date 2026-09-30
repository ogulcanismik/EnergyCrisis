# Unity Editor watchdog (EnergyCrisis)

## Start / stop

- **Start:** `Tools\Watchdog\Start-UnityWatchdog.bat`
- **Stop:** create `Tools\Watchdog\STOP` or run `Stop-UnityWatchdog.bat` (does not kill Unity)

## Behavior

- Polls for an interactive Editor opened on EnergyCrisis (`-projectPath`, not `-batchmode`).
- Treats a process as healthy only when working set is above ~200 MB and the window is not the admin warning dialog.
- After relaunch, auto-dismisses **“I wish to continue at my own risk”** on the *Unity is running as administrator* dialog (otherwise relaunches stick at ~59 MB).
- Caps: 5 relaunches / rolling hour, then 10 min cooldown.

## If relaunch loops

1. Create `Tools\Watchdog\STOP`.
2. Kill stuck `Unity.exe` stubs if needed.
3. Launch Editor once; if admin dialog appears, choose continue (or let the watchdog dismiss it).
4. Remove `STOP` and start the watchdog again.
