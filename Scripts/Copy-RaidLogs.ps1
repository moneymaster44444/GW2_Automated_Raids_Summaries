# Auto-populates Raid_Logs from a user-configured arcDPS log folder, filtered
# by the active raid window resolved by Resolve-RaidWindow.ps1.
#
# Clears any existing .zevtc / .evtc files from DestDir, then walks SourceDir
# recursively and copies every .zevtc / .evtc whose LastWriteTime falls inside
# [WindowStart, WindowEnd] and whose size is strictly greater than MinSizeKB.
#
# Designed to be safe to call on every run: a missing source folder just logs
# a warning and skips, so the pipeline can fall back to whatever is already in
# Raid_Logs.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$SourceDir,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$DestDir,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$WindowStart,
    [Parameter(Mandatory = $true)][ValidateNotNullOrEmpty()][string]$WindowEnd,
    [int]$MinSizeKB = 900
)

$ErrorActionPreference = 'Stop'

# arcDPS writes .zevtc (compressed) by default; .evtc is the uncompressed variant.
$LogExtensions = @('.zevtc', '.evtc')

if (-not (Test-Path -LiteralPath $DestDir)) {
    New-Item -ItemType Directory -Path $DestDir -Force | Out-Null
}

# Always clear prior-run logs so each run starts from a clean slate.
Get-ChildItem -LiteralPath $DestDir -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Extension -in $LogExtensions } |
    Remove-Item -Force -ErrorAction SilentlyContinue

if (-not (Test-Path -LiteralPath $SourceDir)) {
    Write-Host "[WARN] LOG_SOURCE_DIR does not exist: $SourceDir"
    Write-Host "[WARN] Skipping auto-copy; Raid_Logs is empty."
    exit 0
}

$invariant = [System.Globalization.CultureInfo]::InvariantCulture
$start = [DateTime]::Parse($WindowStart, $invariant)
$end   = [DateTime]::Parse($WindowEnd,   $invariant)
$minBytes = [long]$MinSizeKB * 1024

# @( ... ) keeps a single match as an array so the foreach below behaves consistently.
$logFiles = @(
    Get-ChildItem -LiteralPath $SourceDir -Recurse -File -ErrorAction SilentlyContinue |
        Where-Object {
            $_.Extension -in $LogExtensions -and
            $_.LastWriteTime -ge $start -and
            $_.LastWriteTime -le $end -and
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

Write-Host ("[INFO] Auto-copied {0} log file(s) from '{1}' (window {2:yyyy-MM-dd HH:mm} -> {3:yyyy-MM-dd HH:mm}, > {4} KB)." -f $copied, $SourceDir, $start, $end, $MinSizeKB)
exit 0
