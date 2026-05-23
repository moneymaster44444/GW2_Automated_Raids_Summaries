# Runs Elite Insights against every .zevtc / .evtc in -LogsDir, up to
# -MaxParallel instances at once (0 = auto, ~half the CPU cores). Renders a
# live status table when stdout is attached to a real terminal, and falls
# back to plain line-by-line output when it isn't.
#
# Exit codes: 0 = all succeeded, 1 = at least one EI invocation failed,
# 2 = bad parameters, 10 = no logs found.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$EiExe,
    [Parameter(Mandatory = $true)][string]$EiConf,
    [Parameter(Mandatory = $true)][string]$LogsDir,
    [int]$MaxParallel = 0
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $EiExe)) {
    Write-Error "EI executable not found: $EiExe"
    exit 2
}
if (-not (Test-Path -LiteralPath $EiConf)) {
    Write-Error "EI config not found: $EiConf"
    exit 2
}
if (-not (Test-Path -LiteralPath $LogsDir)) {
    Write-Error "Logs directory not found: $LogsDir"
    exit 2
}

$logs = @(
    Get-ChildItem -LiteralPath $LogsDir -File -Filter '*.zevtc' -ErrorAction SilentlyContinue
    Get-ChildItem -LiteralPath $LogsDir -File -Filter '*.evtc'  -ErrorAction SilentlyContinue
) | Sort-Object -Property FullName -Unique

if (-not $logs -or $logs.Count -eq 0) {
    Write-Host "[INFO] No .zevtc or .evtc files found in: $LogsDir"
    exit 10
}

if ($MaxParallel -le 0) {
    $cores = [Environment]::ProcessorCount
    if ($cores -lt 2) { $cores = 2 }
    $MaxParallel = [Math]::Max(2, [Math]::Floor($cores / 2))
}
if ($MaxParallel -gt $logs.Count) { $MaxParallel = $logs.Count }

# ---------------------------------------------------------------------------
# Terminal / VT setup
#
# Live updates use VT escape codes with *relative* cursor moves (\e[NA up).
# Absolute Win32 SetCursorPosition coordinates get re-anchored to the visible
# window by conpty (the pseudo-console powering Windows Terminal) and desync
# the moment content scrolls.
# ---------------------------------------------------------------------------
$script:Interactive  = -not [Console]::IsOutputRedirected
$script:ConsoleWidth = 80
try {
    $w = [Console]::WindowWidth
    if ($w -gt 0) { $script:ConsoleWidth = $w }
} catch {
    $script:Interactive = $false
}

$ESC = [char]27
$C_RESET   = "$ESC[0m"
$C_GREEN   = "$ESC[92m"
$C_YELLOW  = "$ESC[93m"
$C_RED     = "$ESC[91m"
$C_MAGENTA = "$ESC[95m"

$script:Total    = $logs.Count
$script:Done     = 0
$script:Failed   = 0
$script:Failures = New-Object System.Collections.Generic.List[object]
$script:Running  = New-Object System.Collections.Generic.List[object]

# ---------------------------------------------------------------------------
# dps.report /uploadContent rate limit: 25 requests per 60 seconds.
# Each EI invocation performs exactly one upload, so we cap process launches
# to 25 per rolling 60s window whenever parallelism is on. Single-threaded
# runs (MaxParallel == 1) bypass this since one upload at a time can't exceed
# the limit on any realistic hardware.
# See https://dps.report/api
# ---------------------------------------------------------------------------
$script:RateLimitEnabled    = ($MaxParallel -ne 1)
$script:RateLimitMax        = 25
$script:RateLimitWindowSec  = 60
$script:LaunchTimestamps    = New-Object System.Collections.Generic.Queue[DateTime]

# ---------------------------------------------------------------------------
# Rendering helpers
# ---------------------------------------------------------------------------

# Truncate a log name so the rendered line fits on one console row. If a row
# wraps the cursor's relative position desyncs from what we wrote.
function Format-LogName([string]$name) {
    $maxNameLen = $script:ConsoleWidth - 14
    if ($maxNameLen -lt 10) { $maxNameLen = 10 }
    if ($name.Length -gt $maxNameLen) {
        return $name.Substring(0, $maxNameLen - 3) + '...'
    }
    return $name
}

function Format-StatusLine([string]$status, [string]$colorSeq, [string]$logName) {
    return ("    [{0}{1}{2}] {3}" -f $colorSeq, $status.PadRight(6), $C_RESET, (Format-LogName $logName))
}

function Format-ProgressBar([int]$done, [int]$total) {
    $width = 30
    if ($total -le 0) {
        $filled = 0
        $pct = 0
    } else {
        $filled = [Math]::Floor($width * $done / $total)
        $pct = [Math]::Floor(100 * $done / $total)
    }
    return ("[{0}{1}{2}{3}] {4}/{5} ({6}%)" -f `
        $C_GREEN, ('#' * $filled), $C_RESET, ('-' * ($width - $filled)), $done, $total, $pct)
}

# After the initial render the cursor sits on the line directly below the
# progress bar. From that anchor the progress bar is 1 line up and log row
# $i is ($Total + 1 - $i) lines up. Update-Row / Update-Progress save the
# cursor (\e7), move up, clear and write, then restore (\e8).
function Update-Row([int]$logIndex, [string]$status, [string]$colorSeq, [string]$logName) {
    $up = $script:Total + 1 - $logIndex
    $line = Format-StatusLine $status $colorSeq $logName
    [Console]::Write("${ESC}7${ESC}[${up}A`r${ESC}[2K${line}${ESC}8")
}

function Update-Progress() {
    $bar = Format-ProgressBar $script:Done $script:Total
    [Console]::Write("${ESC}7${ESC}[1A`r${ESC}[2K${bar}${ESC}8")
}

# Single entry point for state changes. In interactive mode it updates the
# row in place; otherwise it prints a one-shot log line.
function Report-Status([int]$logIndex, [string]$status, [string]$colorSeq, [string]$logName) {
    if ($script:Interactive) {
        Update-Row $logIndex $status $colorSeq $logName
    } else {
        $progress = if ($status -eq 'start') { $logIndex + 1 } else { $script:Done }
        Write-Host ("    [{0}] ({1}/{2}) {3}" -f $status.PadRight(5), $progress, $script:Total, $logName)
    }
}

if ($script:RateLimitEnabled) {
    Write-Host ("[INFO] Throttling EI launches to {0}/{1}s to respect dps.report /uploadContent rate limit." -f $script:RateLimitMax, $script:RateLimitWindowSec)
}

# ---------------------------------------------------------------------------
# Rate limiter
# ---------------------------------------------------------------------------

# Block until the rolling 60s window can admit another /uploadContent POST,
# then record this launch's timestamp. UTC for monotonicity across DST shifts.
function Wait-RateLimit {
    if (-not $script:RateLimitEnabled) { return }
    $announced = $false
    while ($script:LaunchTimestamps.Count -ge $script:RateLimitMax) {
        $oldest = $script:LaunchTimestamps.Peek()
        $elapsed = ([DateTime]::UtcNow - $oldest).TotalSeconds
        if ($elapsed -ge $script:RateLimitWindowSec) {
            [void]$script:LaunchTimestamps.Dequeue()
            continue
        }
        if (-not $announced -and -not $script:Interactive) {
            $remaining = $script:RateLimitWindowSec - $elapsed
            Write-Host ("    [RATE] Pausing ~{0:0.0}s (25 uploads in the last 60s)..." -f $remaining)
            $announced = $true
        }
        Start-Sleep -Milliseconds 500
    }
    $script:LaunchTimestamps.Enqueue([DateTime]::UtcNow)
}

# ---------------------------------------------------------------------------
# Job pump
# ---------------------------------------------------------------------------

# Block until at least one running EI process exits, then book-keep its
# result (UI update, progress tick, capture stderr tail on failure).
function Drain-One {
    while ($true) {
        for ($i = 0; $i -lt $script:Running.Count; $i++) {
            $job = $script:Running[$i]
            if (-not $job.Process.HasExited) { continue }

            $script:Running.RemoveAt($i)
            $script:Done++
            # Forces async stdout/stderr redirection to flush to disk before
            # we read the err file below.
            $job.Process.WaitForExit()
            $exitCode = $job.Process.ExitCode

            if ($exitCode -eq 0) {
                Report-Status $job.LogIndex 'done' $C_GREEN $job.LogName
            } else {
                $script:Failed++
                Report-Status $job.LogIndex 'FAIL' $C_RED $job.LogName
                if (-not $script:Interactive) {
                    Write-Host ("    [WARN] EI returned exit {0} for {1}" -f $exitCode, $job.LogName)
                }
                $tail = @()
                if ((Test-Path -LiteralPath $job.ErrFile) -and ((Get-Item -LiteralPath $job.ErrFile).Length -gt 0)) {
                    $tail = Get-Content -LiteralPath $job.ErrFile -Tail 20
                }
                $script:Failures.Add([pscustomobject]@{
                    Name = $job.LogName
                    Exit = $exitCode
                    Tail = $tail
                })
            }

            if ($script:Interactive) { Update-Progress }

            Remove-Item -ErrorAction SilentlyContinue -LiteralPath $job.OutFile
            Remove-Item -ErrorAction SilentlyContinue -LiteralPath $job.ErrFile
            return
        }
        Start-Sleep -Milliseconds 200
    }
}

# ---------------------------------------------------------------------------
# Initial render + main loop
# ---------------------------------------------------------------------------

Write-Host ("[INFO] Parsing {0} log(s) with up to {1} parallel EI instance(s)..." -f $script:Total, $MaxParallel)

if ($script:Interactive) {
    foreach ($log in $logs) {
        [Console]::WriteLine((Format-StatusLine 'queued' $C_MAGENTA $log.Name))
    }
    [Console]::WriteLine((Format-ProgressBar 0 $script:Total))
    # The WriteLine above leaves the cursor on the line immediately below the
    # progress bar. That is the anchor Update-Row / Update-Progress measure
    # against -- do NOT emit a stray blank WriteLine here or every update
    # will land one row too low.

    # Hold the all-queued frame so the user sees magenta even when
    # MaxParallel >= log count and every row would otherwise flip in the
    # same millisecond.
    Start-Sleep -Milliseconds 500
}

for ($idx = 0; $idx -lt $script:Total; $idx++) {
    while ($script:Running.Count -ge $MaxParallel) {
        Drain-One
    }

    $log = $logs[$idx]

    Wait-RateLimit

    $outFile = [System.IO.Path]::GetTempFileName()
    $errFile = "$outFile.err"

    Report-Status $idx 'start' $C_YELLOW $log.Name

    $proc = Start-Process -FilePath $EiExe `
        -ArgumentList @('-c', "`"$EiConf`"", "`"$($log.FullName)`"") `
        -NoNewWindow -PassThru `
        -RedirectStandardOutput $outFile `
        -RedirectStandardError  $errFile

    # Touch .Handle so PowerShell keeps the Win32 handle open; otherwise the
    # handle is released as soon as Start-Process returns and ExitCode reads
    # back as $null even after the process exits cleanly.
    $null = $proc.Handle

    $script:Running.Add([pscustomobject]@{
        Process  = $proc
        LogName  = $log.Name
        LogIndex = $idx
        OutFile  = $outFile
        ErrFile  = $errFile
    })
}

while ($script:Running.Count -gt 0) {
    Drain-One
}

Write-Host ("[INFO] Parallel EI parsing complete. {0} succeeded, {1} failed." -f ($script:Total - $script:Failed), $script:Failed)

foreach ($f in $script:Failures) {
    Write-Host ""
    Write-Host ("[WARN] EI returned exit {0} for {1}:" -f $f.Exit, $f.Name)
    foreach ($line in $f.Tail) { Write-Host "        $line" }
}

if ($script:Failed -gt 0) { exit 1 }
exit 0
