@echo off
REM Stop the EnergyCrisis Unity Editor watchdog (does NOT kill Unity).
set SCRIPT_DIR=%~dp0
echo Creating STOP flag...
echo. > "%SCRIPT_DIR%STOP"
REM Also try to stop the recorded PID if still running
if exist "%SCRIPT_DIR%watchdog.pid" (
  set /p WPID=<"%SCRIPT_DIR%watchdog.pid"
  if defined WPID (
    echo Stopping watchdog PID %WPID% ...
    taskkill /PID %WPID% /F >nul 2>&1
    del /q "%SCRIPT_DIR%watchdog.pid" >nul 2>&1
  )
)
echo Watchdog stop requested. Unity Editor was not force-killed.
exit /b 0
