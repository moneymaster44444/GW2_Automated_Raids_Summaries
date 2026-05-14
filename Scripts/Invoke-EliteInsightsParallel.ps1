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

# Decide if we can do the live UI. Falls back to plain line-by-line output if
# stdout is redirected, the console doesn't expose cursor APIs, or the window
# is too short to fit the full status table without buffer scrolling.
$script:Interactive = $false
$script:ConsoleWidth = 80
try {
    if (-not [Console]::IsOutputRedirected) {
        $null = [Console]::CursorTop
        $script:ConsoleWidth = [Console]::WindowWidth
        if ($script:ConsoleWidth -le 0) { $script:ConsoleWidth = 80 }
        $minHeight = $logs.Count + 5
        if ($minHeight -le [Console]::WindowHeight) {
            $script:Interactive = $true
        }
    }
} catch {
    $script:Interactive = $false
}

Write-Host ("[INFO] Parsing {0} log(s) with up to {1} parallel EI instance(s)..." -f $logs.Count, $MaxParallel)

$script:Total       = $logs.Count
$script:Done        = 0
$script:Failed      = 0
$script:Failures    = New-Object System.Collections.Generic.List[object]
$script:Running     = New-Object System.Collections.Generic.List[object]
$script:StatusRows  = New-Object int[] $logs.Count
$script:ProgressRow = -1
$script:SafeRow     = -1

# Truncate a log name so the rendered line fits on one console row. We need to
# stay within WindowWidth or the cursor wraps and our row-index tracking
# desyncs with the buffer.
function Format-LogName([string]$name) {
    $maxNameLen = $script:ConsoleWidth - 14
    if ($maxNameLen -lt 10) { $maxNameLen = 10 }
    if ($name.Length -gt $maxNameLen) {
        return $name.Substring(0, $maxNameLen - 3) + '...'
    }
    return $name
}

function Write-StatusLine([int]$row, [string]$status, [ConsoleColor]$color, [string]$logName) {
    if (-not $script:Interactive) { return }
    [Console]::SetCursorPosition(0, $row)
    [Console]::Write('    [')
    $orig = [Console]::ForegroundColor
    [Console]::ForegroundColor = $color
    [Console]::Write($status.PadRight(6))
    [Console]::ForegroundColor = $orig
    [Console]::Write('] ')
    [Console]::Write((Format-LogName $logName))
    $remaining = $script:ConsoleWidth - [Console]::CursorLeft - 1
    if ($remaining -gt 0) { [Console]::Write((' ' * $remaining)) }
}

function Write-ProgressBar([int]$row, [int]$done, [int]$total) {
    if (-not $script:Interactive) { return }
    [Console]::SetCursorPosition(0, $row)
    $width = 30
    if ($total -le 0) { $filled = 0; $pct = 0 }
    else {
        $filled = [Math]::Floor($width * $done / $total)
        $pct = [Math]::Floor(100 * $done / $total)
    }
    [Console]::Write('[')
    $orig = [Console]::ForegroundColor
    [Console]::ForegroundColor = 'Green'
    [Console]::Write(('#' * $filled))
    [Console]::ForegroundColor = $orig
    [Console]::Write(('-' * ($width - $filled)))
    [Console]::Write(("] {0}/{1} ({2}%)" -f $done, $total, $pct))
    $remaining = $script:ConsoleWidth - [Console]::CursorLeft - 1
    if ($remaining -gt 0) { [Console]::Write((' ' * $remaining)) }
}

if ($script:Interactive) {
    for ($i = 0; $i -lt $logs.Count; $i++) {
        $script:StatusRows[$i] = [Console]::CursorTop
        Write-StatusLine $script:StatusRows[$i] 'queued' 'Red' $logs[$i].Name
        [Console]::WriteLine()
    }
    $script:ProgressRow = [Console]::CursorTop
    Write-ProgressBar $script:ProgressRow 0 $logs.Count
    [Console]::WriteLine()
    $script:SafeRow = [Console]::CursorTop
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
                        Write-StatusLine $script:StatusRows[$job.LogIndex] 'done' 'Green' $job.LogName
                    } else {
                        Write-Host ("    [done ] ({0}/{1}) {2}" -f $script:Done, $script:Total, $job.LogName)
                    }
                } else {
                    $script:Failed++
                    if ($script:Interactive) {
                        Write-StatusLine $script:StatusRows[$job.LogIndex] 'FAIL' 'Red' $job.LogName
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

                Write-ProgressBar $script:ProgressRow $script:Done $script:Total

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
        Write-StatusLine $script:StatusRows[$idx] 'start' 'Yellow' $logName
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

if ($script:Interactive) {
    [Console]::SetCursorPosition(0, $script:SafeRow)
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
