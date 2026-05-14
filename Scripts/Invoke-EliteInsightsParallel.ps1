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

Write-Host ("[INFO] Parsing {0} log(s) with up to {1} parallel EI instance(s)..." -f $logs.Count, $MaxParallel)

$running = New-Object System.Collections.Generic.List[object]
$failed  = 0
$started = 0
$done    = 0

function Drain-One {
    param([bool]$Blocking)
    while ($true) {
        for ($i = 0; $i -lt $script:running.Count; $i++) {
            $job = $script:running[$i]
            if ($job.Process.HasExited) {
                $script:running.RemoveAt($i)
                $script:done++
                # Forces async stdout/stderr redirection to flush to disk
                # before we read the err file below.
                $job.Process.WaitForExit()
                $exitCode = $job.Process.ExitCode
                $tag = if ($exitCode -eq 0) { 'done ' } else { 'FAIL ' }
                Write-Host ("    [{0}] ({1}/{2}) {3}" -f $tag, $script:done, $logs.Count, $job.LogName)
                if ($exitCode -ne 0) {
                    $script:failed++
                    Write-Host ("    [WARN] EI returned exit {0} for {1}" -f $exitCode, $job.LogName)
                    if ((Test-Path -LiteralPath $job.ErrFile) -and ((Get-Item -LiteralPath $job.ErrFile).Length -gt 0)) {
                        Get-Content -LiteralPath $job.ErrFile -Tail 20 | ForEach-Object { Write-Host "        $_" }
                    }
                }
                Remove-Item -ErrorAction SilentlyContinue -LiteralPath $job.OutFile
                Remove-Item -ErrorAction SilentlyContinue -LiteralPath $job.ErrFile
                return $true
            }
        }
        if (-not $Blocking) { return $false }
        Start-Sleep -Milliseconds 200
    }
}

foreach ($log in $logs) {
    while ($running.Count -ge $MaxParallel) {
        [void](Drain-One -Blocking $true)
    }

    $started++
    $logName = $log.Name
    $outFile = [System.IO.Path]::GetTempFileName()
    $errFile = "$outFile.err"

    Write-Host ("    [start] ({0}/{1}) {2}" -f $started, $logs.Count, $logName)

    $proc = Start-Process -FilePath $EiExe `
        -ArgumentList @('-c', "`"$EiConf`"", "`"$($log.FullName)`"") `
        -NoNewWindow -PassThru `
        -RedirectStandardOutput $outFile `
        -RedirectStandardError  $errFile

    # Touch .Handle so PowerShell keeps the Win32 handle open; otherwise the
    # handle is released as soon as Start-Process returns and ExitCode reads
    # back as $null even after the process exits cleanly.
    $null = $proc.Handle

    $running.Add([pscustomobject]@{
        Process = $proc
        LogName = $logName
        OutFile = $outFile
        ErrFile = $errFile
    })
}

while ($running.Count -gt 0) {
    [void](Drain-One -Blocking $true)
}

Write-Host ("[INFO] Parallel EI parsing complete. {0} succeeded, {1} failed." -f ($logs.Count - $failed), $failed)

if ($failed -gt 0) { exit 1 }
exit 0
