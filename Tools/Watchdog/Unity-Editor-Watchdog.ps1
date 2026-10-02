#Requires -Version 5.1
<#
.SYNOPSIS
  Auto-relaunch Unity Editor for EnergyCrisis after a crash.

.DESCRIPTION
  Polls for a healthy interactive Unity Editor on this project
  (command line contains -projectPath matching EnergyCrisis, NOT -batchmode,
  working set > ~200MB, not stuck on the admin warning dialog).
  Auto-dismisses the "running as administrator" dialog after relaunch.
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
$MinHealthyBytes = 200MB

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
    $fallback = 'C:\Program Files\Unity\Hub\Editor\6000.3.10f1\Editor\Unity.exe'
    if (Test-Path $fallback) { return $fallback }
    return $null
}

function Get-EnergyCrisisEditorProcesses {
    param([string]$Root)
    $normalized = ($Root -replace '/', '\').TrimEnd('\').ToLowerInvariant()
    $needle = 'energycrisis'
    $list = @()
    try {
        $procs = Get-CimInstance Win32_Process -Filter "Name='Unity.exe'" -ErrorAction Stop
    }
    catch {
        Write-Log "Failed to query processes: $_" 'WARN'
        return $list
    }

    foreach ($p in $procs) {
        $cmd = $p.CommandLine
        if (-not $cmd) { continue }
        if ($cmd -match '(?i)(-batchmode|-batchMode)') { continue }
        $hasProject =
            ($cmd -match '(?i)-projectpath\s+"?([^"\s]+)"?' -and (
                ($Matches[1] -replace '/', '\').TrimEnd('\').ToLowerInvariant() -eq $normalized -or
                ($Matches[1] -replace '/', '\').ToLowerInvariant().Contains($needle)
            )) -or
            ($cmd.ToLowerInvariant().Contains($needle) -and $cmd -match '(?i)-projectpath')
        if ($hasProject) { $list += $p }
    }
    return $list
}

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
using System.Text;
public static class MoPWatchdogWin {
  public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);
  [DllImport("user32.dll")] public static extern bool EnumChildWindows(IntPtr hWnd, EnumWindowsProc callback, IntPtr lParam);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetWindowText(IntPtr hWnd, StringBuilder sb, int maxCount);
  [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern int GetClassName(IntPtr hWnd, StringBuilder sb, int maxCount);
  [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);
  public const uint BM_CLICK = 0x00F5;
}
"@ -ErrorAction SilentlyContinue

function Dismiss-UnityAdminDialog {
    param([int[]]$ProcessIds)
    if (-not $ProcessIds -or $ProcessIds.Count -eq 0) { return $false }
    $dismissed = $false
    $targetPids = New-Object 'System.Collections.Generic.HashSet[uint32]'
    foreach ($id in $ProcessIds) { [void]$targetPids.Add([uint32]$id) }

    $null = [MoPWatchdogWin]::EnumWindows({
        param($hWnd, $lParam)
        $procId = [uint32]0
        [MoPWatchdogWin]::GetWindowThreadProcessId($hWnd, [ref]$procId) | Out-Null
        if (-not $targetPids.Contains($procId)) { return $true }
        $title = New-Object System.Text.StringBuilder 512
        [MoPWatchdogWin]::GetWindowText($hWnd, $title, 512) | Out-Null
        $t = $title.ToString()
        if ($t -notmatch '(?i)running as administrator') { return $true }

        $script:adminButtons = @()
        $null = [MoPWatchdogWin]::EnumChildWindows($hWnd, {
            param($ch, $lp)
            $cls = New-Object System.Text.StringBuilder 256
            [MoPWatchdogWin]::GetClassName($ch, $cls, 256) | Out-Null
            if ($cls.ToString() -ne 'Button') { return $true }
            $txt = New-Object System.Text.StringBuilder 256
            [MoPWatchdogWin]::GetWindowText($ch, $txt, 256) | Out-Null
            $script:adminButtons += [pscustomobject]@{ H = $ch; Text = $txt.ToString() }
            return $true
        }, [IntPtr]::Zero)

        $ok = $script:adminButtons | Where-Object { $_.Text -match '(?i)own risk|continue' } | Select-Object -First 1
        if ($ok) {
            [MoPWatchdogWin]::SendMessage($ok.H, [MoPWatchdogWin]::BM_CLICK, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
            $script:dismissedFlag = $true
            Write-Log "Dismissed admin dialog on PID $procId ($($ok.Text))."
        }
        return $true
    }, [IntPtr]::Zero)

    return [bool]$script:dismissedFlag
}

function Test-EnergyCrisisEditorHealthy {
    param([string]$Root)
    $list = @(Get-EnergyCrisisEditorProcesses -Root $Root)
    if ($list.Count -eq 0) { return $false }

    $pids = @($list | ForEach-Object { [int]$_.ProcessId })
    [void](Dismiss-UnityAdminDialog -ProcessIds $pids)

    foreach ($p in $list) {
        $proc = Get-Process -Id $p.ProcessId -ErrorAction SilentlyContinue
        if (-not $proc) { continue }
        if ($proc.WorkingSet64 -lt $MinHealthyBytes) { continue }
        if ($proc.MainWindowTitle -match '(?i)^Unity is running as administrator') { continue }
        return $true
    }
    return $false
}

function Start-EnergyCrisisEditor {
    param([string]$UnityExe, [string]$Root)
    $args = @('-projectPath', $Root)
    Write-Log "Launching: `"$UnityExe`" $($args -join ' ')"
    Start-Process -FilePath $UnityExe -ArgumentList $args | Out-Null
}

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
Write-Log "Healthy = WS>$MinHealthyBytes and not admin-dialog stuck; auto-dismiss enabled."
Write-Log "Stop: create file $StopFlag  OR  run Stop-UnityWatchdog.bat"

try {
    while ($true) {
        if (Test-Path $StopFlag) {
            Write-Log 'STOP flag detected — exiting cleanly.'
            break
        }

        if (Test-EnergyCrisisEditorHealthy -Root $ProjectPath) {
            Start-Sleep -Seconds $PollSeconds
            continue
        }

        # Still may have a hung stub — try dismiss before declaring down
        $stubs = @(Get-EnergyCrisisEditorProcesses -Root $ProjectPath)
        if ($stubs.Count -gt 0) {
            [void](Dismiss-UnityAdminDialog -ProcessIds (@($stubs | ForEach-Object { [int]$_.ProcessId })))
            Start-Sleep -Seconds 5
            if (Test-EnergyCrisisEditorHealthy -Root $ProjectPath) {
                Write-Log 'Editor became healthy after admin-dialog dismiss.'
                continue
            }
        }

        Write-Log 'EnergyCrisis interactive Editor not healthy — waiting grace period before relaunch.'
        Start-Sleep -Seconds $GraceSeconds

        if (Test-Path $StopFlag) {
            Write-Log 'STOP flag during grace — exiting without relaunch.'
            break
        }

        if (Test-EnergyCrisisEditorHealthy -Root $ProjectPath) {
            Write-Log 'Editor came back during grace — no relaunch.'
            continue
        }

        $cutoff = (Get-Date).AddHours(-1)
        while ($relaunchTimes.Count -gt 0 -and $relaunchTimes[0] -lt $cutoff) {
            $relaunchTimes.RemoveAt(0)
        }

        if ($relaunchTimes.Count -ge $MaxRelaunchesPerHour) {
            Write-Log "Relaunch cap hit ($MaxRelaunchesPerHour/hour). Cooling down 10 minutes." 'WARN'
            Start-Sleep -Seconds 600
            continue
        }

        # Kill stuck sub-200MB stubs so relaunch can bind the project
        foreach ($s in @(Get-EnergyCrisisEditorProcesses -Root $ProjectPath)) {
            $gp = Get-Process -Id $s.ProcessId -ErrorAction SilentlyContinue
            if ($gp -and $gp.WorkingSet64 -lt $MinHealthyBytes) {
                Write-Log "Killing stuck Unity stub PID $($s.ProcessId) (WS=$([math]::Round($gp.WorkingSet64/1MB,1))MB)." 'WARN'
                Stop-Process -Id $s.ProcessId -Force -ErrorAction SilentlyContinue
            }
        }
        Start-Sleep -Seconds 2

        Start-EnergyCrisisEditor -UnityExe $UnityExe -Root $ProjectPath
        $relaunchTimes.Add((Get-Date))
        Write-Log "Relaunch #$(($relaunchTimes.Count)) in current hour window."

        # Dismiss admin dialog during boot window
        for ($i = 0; $i -lt 12; $i++) {
            Start-Sleep -Seconds 5
            $boot = @(Get-EnergyCrisisEditorProcesses -Root $ProjectPath)
            if ($boot.Count -gt 0) {
                [void](Dismiss-UnityAdminDialog -ProcessIds (@($boot | ForEach-Object { [int]$_.ProcessId })))
            }
            if (Test-EnergyCrisisEditorHealthy -Root $ProjectPath) { break }
        }
    }
}
finally {
    if ((Test-Path $PidFile) -and ((Get-Content $PidFile -ErrorAction SilentlyContinue) -eq "$PID")) {
        Remove-Item $PidFile -Force -ErrorAction SilentlyContinue
    }
    Write-Log "Watchdog stopped PID=$PID"
}
