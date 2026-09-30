#Requires -Version 5.1
<#
.SYNOPSIS
  Auto-relaunch Unity Editor for EnergyCrisis after a crash.

.DESCRIPTION
  Polls for an interactive Unity Editor process opened on this project
  (command line contains -projectPath matching EnergyCrisis, and is NOT
  -batchmode). Does not kill healthy editors. Caps relaunches to avoid loops.

.PARAMETER ProjectPath
  Absolute path to the Unity project root.

.PARAMETER PollSeconds
  How often to check for a live Editor (default 20).

.PARAMETER GraceSeconds
  Extra wait after detecting "down" before relaunch (default 20).

.PARAMETER MaxRelaunchesPerHour
  Soft cap on relaunches in any rolling 60-minute window (default 5).

.PARAMETER LogPath
  Log file path (default: Tools\Watchdog\watchdog.log next to this script).
#>
[CmdletBinding()]
param(
    [string]$ProjectPath = 'C:\Users\Ogulcan\Unity\EnergyCrisis',
    [int]$PollSeconds = 20,
    [int]$GraceSeconds = 20,
    [int]$MaxRelaunchesPerHour = 5,
    [string]$LogPath = ''
)

$ErrorActionPreference = 'Continue'
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
if (-not $LogPath) {
    $LogPath = Join-Path $ScriptDir 'watchdog.log'
}
$StopFlag = Join-Path $ScriptDir 'STOP'
$PidFile = Join-Path $ScriptDir 'watchdog.pid'

function Write-Log {
    param([string]$Message, [string]$Level = 'INFO')
    $line = '{0} [{1}] {2}' -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'), $Level, $Message
    Add-Content -Path $LogPath -Value $line -Encoding UTF8
    Write-Host $line
}

function Get-ProjectVersion {
    param([string]$Root)
    $verFile = Join-Path $Root 'ProjectSettings\ProjectVersion.txt'
    if (-not (Test-Path $verFile)) { return $null }
    $line = Get-Content $verFile | Where-Object { $_ -match 'm_EditorVersion:\s*(.+)' } | Select-Object -First 1
    if ($line -match 'm_EditorVersion:\s*(.+)') { return $Matches[1].Trim() }
    return $null
}

function Resolve-UnityEditor {
    param([string]$Root)
    $version = Get-ProjectVersion -Root $Root
    if ($version) {
        $candidate = "C:\Program Files\Unity\Hub\Editor\$version\Editor\Unity.exe"
        if (Test-Path $candidate) { return $candidate }
    }
    # Fallback: known probe path
    $fallback = 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe'
    if (Test-Path $fallback) { return $fallback }
    return $null
}

function Test-EnergyCrisisEditorRunning {
    param([string]$Root)
    $normalized = ($Root -replace '/', '\').TrimEnd('\').ToLowerInvariant()
    $needle = 'energycrisis'

    try {
        $procs = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction Stop
    }
    catch {
        Write-Log "Failed to query processes: $_" 'WARN'
        return $false
    }

    foreach ($p in $procs) {
        $cmd = $p.CommandLine
        if (-not $cmd) { continue }

        # Skip batchmode workers / CLI (AssetImportWorker, etc.)
        if ($cmd -match '(?i)(-batchmode|-batchMode)') { continue }

        # Prefer -projectPath / -projectpath containing EnergyCrisis or exact path
        $hasProject =
            ($cmd -match '(?i)-projectpath\s+"?([^"\s]+)"?' -and (
                ($Matches[1] -replace '/', '\').TrimEnd('\').ToLowerInvariant() -eq $normalized -or
                ($Matches[1] -replace '/', '\').ToLowerInvariant().Contains($needle)
            )) -or
            ($cmd.ToLowerInvariant().Contains($needle) -and $cmd -match '(?i)-projectpath')

        if ($hasProject) {
            return $true
        }
    }
    return $false
}

function Start-EnergyCrisisEditor {
    param([string]$UnityExe, [string]$Root)
    # Interactive Editor only — never -batchmode (MCP + UI workflow)
    $args = @('-projectPath', $Root)
    Write-Log "Launching: `"$UnityExe`" $($args -join ' ')"
    Start-Process -FilePath $UnityExe -ArgumentList $args | Out-Null
}

# --- main ---
$ProjectPath = [System.IO.Path]::GetFullPath($ProjectPath)
if (-not (Test-Path $ProjectPath)) {
    Write-Error "Project path not found: $ProjectPath"
    exit 1
}

$UnityExe = Resolve-UnityEditor -Root $ProjectPath
if (-not $UnityExe) {
    Write-Error 'Could not find Unity Editor binary matching ProjectVersion.txt'
    exit 1
}

# Single-instance guard
if (Test-Path $PidFile) {
    $oldPid = Get-Content $PidFile -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($oldPid -and (Get-Process -Id $oldPid -ErrorAction SilentlyContinue)) {
        Write-Host "Watchdog already running (PID $oldPid). Exiting."
        exit 0
    }
}
$PID | Set-Content -Path $PidFile -Encoding ASCII
if (Test-Path $StopFlag) { Remove-Item $StopFlag -Force }

$relaunchTimes = [System.Collections.Generic.List[datetime]]::new()
$version = Get-ProjectVersion -Root $ProjectPath

Write-Log "Watchdog started PID=$PID project=$ProjectPath editor=$UnityExe version=$version"
Write-Log "Poll=${PollSeconds}s grace=${GraceSeconds}s maxRelaunches/hour=$MaxRelaunchesPerHour"
Write-Log "Stop: create file $StopFlag  OR  run Stop-UnityWatchdog.bat"

try {
    while ($true) {
        if (Test-Path $StopFlag) {
            Write-Log 'STOP flag detected — exiting cleanly.'
            break
        }

        if (Test-EnergyCrisisEditorRunning -Root $ProjectPath) {
            Start-Sleep -Seconds $PollSeconds
            continue
        }

        Write-Log 'EnergyCrisis interactive Editor not found — waiting grace period before relaunch.'
        Start-Sleep -Seconds $GraceSeconds

        if (Test-Path $StopFlag) {
            Write-Log 'STOP flag during grace — exiting without relaunch.'
            break
        }

        if (Test-EnergyCrisisEditorRunning -Root $ProjectPath) {
            Write-Log 'Editor came back during grace — no relaunch.'
            continue
        }

        # Prune relaunch window
        $cutoff = (Get-Date).AddHours(-1)
        while ($relaunchTimes.Count -gt 0 -and $relaunchTimes[0] -lt $cutoff) {
            $relaunchTimes.RemoveAt(0)
        }

        if ($relaunchTimes.Count -ge $MaxRelaunchesPerHour) {
            Write-Log "Relaunch cap hit ($MaxRelaunchesPerHour/hour). Cooling down 10 minutes." 'WARN'
            Start-Sleep -Seconds 600
            continue
        }

        Start-EnergyCrisisEditor -UnityExe $UnityExe -Root $ProjectPath
        $relaunchTimes.Add((Get-Date))
        Write-Log "Relaunch #$(($relaunchTimes.Count)) in current hour window."

        # Give Editor time to appear before next poll
        Start-Sleep -Seconds ([Math]::Max($PollSeconds, 30))
    }
}
finally {
    if ((Test-Path $PidFile) -and ((Get-Content $PidFile -ErrorAction SilentlyContinue) -eq "$PID")) {
        Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
    }
    Write-Log "Watchdog stopped PID=$PID"
}
