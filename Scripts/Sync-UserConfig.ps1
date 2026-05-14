# Migrates new keys from sample.config.txt into an existing config.txt.
#
# Existing keys and any user-edited values are left untouched. Any KEY=VALUE
# entry that appears in the sample but not in the user file is appended to
# the bottom of the user file, along with the comment lines that immediately
# precede it in the sample (so the user sees the same documentation).
#
# Designed to be safe to run on every invocation: when nothing is missing it
# is a no-op.
#
# Exit codes: 0 = success (no-op or migration completed), 2 = bad parameters.

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$SamplePath,
    [Parameter(Mandatory = $true)][string]$UserPath
)

$ErrorActionPreference = 'Stop'

if (-not (Test-Path -LiteralPath $SamplePath)) {
    Write-Error "Sample config not found: $SamplePath"
    exit 2
}
if (-not (Test-Path -LiteralPath $UserPath)) {
    # No user config to migrate. The caller is responsible for the
    # initial-bootstrap copy from sample to user.
    exit 0
}

$keyPattern = '^\s*([A-Za-z_][A-Za-z0-9_]*)\s*='

# Walk the sample collecting (Key, Line, Lead) tuples. Lead is the block of
# comment / blank lines that immediately precede the KEY=VALUE line.
$entries = New-Object System.Collections.Generic.List[object]
$pending = New-Object System.Collections.Generic.List[string]
foreach ($line in Get-Content -LiteralPath $SamplePath) {
    $m = [regex]::Match($line, $keyPattern)
    if ($m.Success) {
        $entries.Add([pscustomobject]@{
            Key  = $m.Groups[1].Value
            Line = $line
            Lead = @($pending)
        })
        $pending.Clear()
    } else {
        $pending.Add($line)
    }
}

# Collect the user's existing keys (case-insensitive).
$userLines = @(Get-Content -LiteralPath $UserPath)
$userKeys = New-Object System.Collections.Generic.HashSet[string]([System.StringComparer]::OrdinalIgnoreCase)
foreach ($line in $userLines) {
    $m = [regex]::Match($line, $keyPattern)
    if ($m.Success) { [void]$userKeys.Add($m.Groups[1].Value) }
}

$missing = @($entries | Where-Object { -not $userKeys.Contains($_.Key) })
if ($missing.Count -eq 0) { exit 0 }

$toAppend = New-Object System.Collections.Generic.List[string]
# Make sure there's separation between the user's existing content and the
# new block we're appending.
$lastNonEmpty = $userLines | Where-Object { $_.Trim().Length -gt 0 } | Select-Object -Last 1
if ($null -ne $lastNonEmpty) { $toAppend.Add('') }
$toAppend.Add('# --- Added by update: new config fields ---')

foreach ($entry in $missing) {
    foreach ($lead in $entry.Lead) { $toAppend.Add($lead) }
    $toAppend.Add($entry.Line)
}

# Match the encoding used by Establish-Configs.ps1 (UTF-8 without BOM).
$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
$existing  = [System.IO.File]::ReadAllText($UserPath, $utf8NoBom)
$prefix    = if ($existing.EndsWith("`n")) { '' } else { "`r`n" }
$payload   = $prefix + ($toAppend -join "`r`n") + "`r`n"
[System.IO.File]::AppendAllText($UserPath, $payload, $utf8NoBom)

$keys = ($missing | ForEach-Object { $_.Key }) -join ', '
Write-Host ("[SETUP] Added {0} new config field(s) to config.txt: {1}" -f $missing.Count, $keys)
exit 0
