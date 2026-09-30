@echo off
REM Start EnergyCrisis Unity Editor crash watchdog in the background.
REM Does not force-kill or relaunch if the Editor is already open on this project.
set SCRIPT_DIR=%~dp0
start "" /MIN powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPT_DIR%Unity-Editor-Watchdog.ps1"
echo Watchdog started (minimized PowerShell). Log: %SCRIPT_DIR%watchdog.log
echo Stop with: %SCRIPT_DIR%Stop-UnityWatchdog.bat
exit /b 0
