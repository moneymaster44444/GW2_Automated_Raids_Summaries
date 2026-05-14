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

# Decide whether to render the live status table. Cursor positioning is done
# with VT escape codes and *relative* moves (up N / down N), which conpty (the
# pseudo-console powering Windows Terminal) handles correctly even when the
# window scrolls. Absolute Win32 SetCursorPosition coords get re-anchored to
# the visible window by conpty and desync when content scrolls -- which is
# why we don't use them.
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

Write-Host ("[INFO] Parsing {0} log(s) with up to {1} parallel EI instance(s)..." -f $logs.Count, $MaxParallel)

$script:Total    = $logs.Count
$script:Done     = 0
$script:Failed   = 0
$script:Failures = New-Object System.Collections.Generic.List[object]
$script:Running  = New-Object System.Collections.Generic.List[object]

# Truncate a log name so the rendered line fits on one console row. If the
# line wraps onto a second row, the cursor's relative position desyncs from
# what we think we wrote.
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
    if ($total -le 0) { $filled = 0; $pct = 0 }
    else {
        $filled = [Math]::Floor($width * $done / $total)
        $pct = [Math]::Floor(100 * $done / $total)
    }
    return ("[{0}{1}{2}{3}] {4}/{5} ({6}%)" -f `
        $C_GREEN, ('#' * $filled), $C_RESET, ('-' * ($width - $filled)), $done, $total, $pct)
}

# After the initial render, the cursor is on a blank line BELOW the progress
# bar. From that anchor:
#   - the progress bar is exactly 1 line above
#   - log row $i (0-indexed) is ($Total + 1 - $i) lines above
# Update-Row / Update-Progress save the cursor, move up to the target row,
# clear it, write the new content, then restore the cursor. `\e7` / `\e8`
# (DEC save/restore cursor) is supported by every modern Windows terminal
# including conpty / Windows Terminal / cmd on Win10+.
function Update-Row([int]$logIndex, [string]$status, [string]$colorSeq, [string]$logName) {
    if (-not $script:Interactive) { return }
    $up = $script:Total + 1 - $logIndex
    $line = Format-StatusLine $status $colorSeq $logName
    [Console]::Write("${ESC}7${ESC}[${up}A`r${ESC}[2K${line}${ESC}8")
}

function Update-Progress() {
    if (-not $script:Interactive) { return }
    $bar = Format-ProgressBar $script:Done $script:Total
    [Console]::Write("${ESC}7${ESC}[1A`r${ESC}[2K${bar}${ESC}8")
}

# Initial render: a full magenta [queued] list followed by an empty progress
# bar. Then a blank "anchor" line where the cursor will rest. Pause briefly
# so the queued state is on screen even when MaxParallel >= logs.Count.
if ($script:Interactive) {
    foreach ($log in $logs) {
        [Console]::WriteLine((Format-StatusLine 'queued' $C_MAGENTA $log.Name))
    }
    # After this WriteLine the cursor sits on the (empty) line directly below
    # the progress bar -- that is the anchor Update-Row / Update-Progress
    # measure their "up N" offsets against. Do NOT add another blank
    # WriteLine here or every update will land one row too low.
    [Console]::WriteLine((Format-ProgressBar 0 $logs.Count))
    Start-Sleep -Milliseconds 500
}

function Drain-One {
    param([bool]$Blocking)
    while ($true) {
        for ($i = 0; $i -lt $script:Running.Count; $i++) {
            $job = $script:Running[$i]
            if ($job.Process.HasExited) {
                $script:Running.RemoveAt($i)
                $script:Done++
                # Forces async stdout/stderr redirection to flush to disk
                # before we read the err file below.
                $job.Process.WaitForExit()
                $exitCode = $job.Process.ExitCode

                if ($exitCode -eq 0) {
                    if ($script:Interactive) {
                        Update-Row $job.LogIndex 'done' $C_GREEN $job.LogName
                    } else {
                        Write-Host ("    [done ] ({0}/{1}) {2}" -f $script:Done, $script:Total, $job.LogName)
                    }
                } else {
                    $script:Failed++
                    if ($script:Interactive) {
                        Update-Row $job.LogIndex 'FAIL' $C_RED $job.LogName
                    } else {
                        Write-Host ("    [FAIL ] ({0}/{1}) {2}" -f $script:Done, $script:Total, $job.LogName)
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

                Update-Progress

                Remove-Item -ErrorAction SilentlyContinue -LiteralPath $job.OutFile
                Remove-Item -ErrorAction SilentlyContinue -LiteralPath $job.ErrFile
                return $true
            }
        }
        if (-not $Blocking) { return $false }
        Start-Sleep -Milliseconds 200
    }
}

for ($idx = 0; $idx -lt $logs.Count; $idx++) {
    while ($script:Running.Count -ge $MaxParallel) {
        [void](Drain-One -Blocking $true)
    }

    $log = $logs[$idx]
    $logName = $log.Name
    $outFile = [System.IO.Path]::GetTempFileName()
    $errFile = "$outFile.err"

    if ($script:Interactive) {
        Update-Row $idx 'start' $C_YELLOW $logName
    } else {
        Write-Host ("    [start] ({0}/{1}) {2}" -f ($idx + 1), $logs.Count, $logName)
    }

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
        LogName  = $logName
        LogIndex = $idx
        OutFile  = $outFile
        ErrFile  = $errFile
    })
}

while ($script:Running.Count -gt 0) {
    [void](Drain-One -Blocking $true)
}

Write-Host ("[INFO] Parallel EI parsing complete. {0} succeeded, {1} failed." -f ($logs.Count - $script:Failed), $script:Failed)

if ($script:Failures.Count -gt 0) {
    foreach ($f in $script:Failures) {
        Write-Host ""
        Write-Host ("[WARN] EI returned exit {0} for {1}:" -f $f.Exit, $f.Name)
        foreach ($line in $f.Tail) { Write-Host "        $line" }
    }
}

if ($script:Failed -gt 0) { exit 1 }
exit 0
