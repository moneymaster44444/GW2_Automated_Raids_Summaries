# Auto-populates Raid_Logs from a user-configured arcDPS log folder.
#
# Clears any existing .zevtc / .evtc files from DestDir, then walks SourceDir
# recursively and copies every .zevtc / .evtc whose LastWriteTime falls on
# today's date and whose size is strictly greater than MinSizeKB.
#
# Designed to be safe to call on every run: missing source folders just skip
# with a warning so the pipeline can fall back to whatever is already in
# Raid_Logs.
#
# Exit codes: 0 = success (including "source missing" and "no matches"),
#             2 = bad parameters.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SourceDir,
    [Parameter(Mandatory = $true)][string]$DestDir,
    [Parameter(Mandatory = $false)][int]$MinSizeKB = 900
)

$ErrorActionPreference = 'Stop'

if ([string]::IsNullOrWhiteSpace($SourceDir)) {
    Write-Error "SourceDir is required."
    exit 2
}
if ([string]::IsNullOrWhiteSpace($DestDir)) {
    Write-Error "DestDir is required."
    exit 2
}

if (-not (Test-Path -LiteralPath $DestDir)) {
    New-Item -ItemType Directory -Path $DestDir -Force | Out-Null
}

# Always clear prior-run logs so each run starts from a clean slate.
Get-ChildItem -LiteralPath $DestDir -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -eq '.zevtc' -or $_.Extension -eq '.evtc' } |
    Remove-Item -Force -ErrorAction SilentlyContinue

if (-not (Test-Path -LiteralPath $SourceDir)) {
    Write-Host "[WARN] LOG_SOURCE_DIR does not exist: $SourceDir"
    Write-Host "[WARN] Skipping auto-copy; Raid_Logs is empty."
    exit 0
}

$today    = (Get-Date).Date
$minBytes = [long]$MinSizeKB * 1024

$logFiles = @(
    Get-ChildItem -LiteralPath $SourceDir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            ($_.Extension -eq '.zevtc' -or $_.Extension -eq '.evtc') -and
            $_.LastWriteTime.Date -eq $today -and
            $_.Length -gt $minBytes
        }
)

$copied = 0
foreach ($file in $logFiles) {
    try {
        Copy-Item -LiteralPath $file.FullName -Destination $DestDir -Force
        $copied++
    } catch {
        Write-Host ("[WARN] Failed to copy '{0}': {1}" -f $file.FullName, $_.Exception.Message)
    }
}

Write-Host ("[INFO] Auto-copied {0} log file(s) from '{1}' (today, > {2} KB)." -f $copied, $SourceDir, $MinSizeKB)
exit 0
