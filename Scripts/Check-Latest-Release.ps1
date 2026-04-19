[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$CurrentVersion,
    [string]$Repo = 'moneymaster44444/GW2_Automated_Raids_Summaries',
    [int]$TimeoutSec = 5
)

$ErrorActionPreference = 'Stop'

function ConvertTo-VersionParts {
    param([string]$Tag)
    if ($null -eq $Tag) { return $null }
    $m = [regex]::Match($Tag, '^v(\d+)\.(\d+)\.(\d+)$')
    if (-not $m.Success) { return $null }
    return ,([int]$m.Groups[1].Value, [int]$m.Groups[2].Value, [int]$m.Groups[3].Value)
}

function Test-UpdateAvailable {
    param([string]$Current, [string]$Latest)
    $curParts = ConvertTo-VersionParts $Current
    $latParts = ConvertTo-VersionParts $Latest
    if ($null -ne $curParts -and $null -ne $latParts) {
        for ($i = 0; $i -lt 3; $i++) {
            if ($latParts[$i] -gt $curParts[$i]) { return $true }
            if ($latParts[$i] -lt $curParts[$i]) { return $false }
        }
        return $false
    }
    return ($Current -ne $Latest)
}

try {
    $headers = @{ 'User-Agent' = 'gw2-raids-updater' }
    $uri = "https://api.github.com/repos/$Repo/releases/latest"
    $resp = Invoke-RestMethod -Uri $uri -Headers $headers -TimeoutSec $TimeoutSec -ErrorAction Stop
    $latest = [string]$resp.tag_name
    if ([string]::IsNullOrWhiteSpace($latest)) { exit 0 }

    $current = $CurrentVersion.Trim()
    $avail = Test-UpdateAvailable -Current $current -Latest $latest

    Write-Output "LATEST=$latest"
    if ($avail) { Write-Output "UPDATE_AVAILABLE=1" } else { Write-Output "UPDATE_AVAILABLE=0" }
    exit 0
}
catch {
    try { [Console]::Error.WriteLine("check-latest-release: $($_.Exception.Message)") } catch { }
    exit 0
}
