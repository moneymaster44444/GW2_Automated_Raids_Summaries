[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)][string]$InstallRoot,
    [string]$Repo = 'moneymaster44444/GW2_Automated_Raids_Summaries',
    [switch]$Yes
)

$ErrorActionPreference = 'Stop'

$InstallRoot = $InstallRoot.Trim().TrimEnd('"').TrimEnd('\', '/')
$InstallRoot = (Resolve-Path -LiteralPath $InstallRoot).Path
$InstallRoot = $InstallRoot.TrimEnd('\', '/')

$PreservePrefixes = @(
    'Raid_Logs',
    'Raids_Summaries',
    'config.txt'
)

$WholesaleReplace = @(
    'Resources\Elite Insights',
    'Resources\EI Combiner'
)

function Test-PreservePath {
    param([string]$RelPath)
    $norm = $RelPath.Replace('/', '\').TrimStart('\')
    foreach ($p in $PreservePrefixes) {
        $prefix = $p.Replace('/', '\').TrimStart('\')
        if ($norm.Equals($prefix, [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
        if ($norm.StartsWith($prefix + '\', [System.StringComparison]::OrdinalIgnoreCase)) { return $true }
    }
    return $false
}

function ConvertTo-VersionParts {
    param([string]$Tag)
    if ($null -eq $Tag) { return $null }
    $m = [regex]::Match($Tag, '^v(\d+)\.(\d+)\.(\d+)$')
    if (-not $m.Success) { return $null }
    return ,([int]$m.Groups[1].Value, [int]$m.Groups[2].Value, [int]$m.Groups[3].Value)
}

function Test-UpgradeAvailable {
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

$staging = $null
try {
    $headers = @{ 'User-Agent' = 'gw2-raids-updater' }
    $apiUri = "https://api.github.com/repos/$Repo/releases/latest"
    Write-Host "[INFO] Checking latest release at $Repo..."
    $rel = Invoke-RestMethod -Uri $apiUri -Headers $headers -TimeoutSec 15 -ErrorAction Stop

    $tag = [string]$rel.tag_name
    $zipUrl = [string]$rel.zipball_url
    if ([string]::IsNullOrWhiteSpace($tag) -or [string]::IsNullOrWhiteSpace($zipUrl)) {
        Write-Host "[ERROR] GitHub response did not include tag_name or zipball_url."
        exit 1
    }

    $versionFile = Join-Path $InstallRoot 'VERSION'
    $current = '(unknown)'
    if (Test-Path -LiteralPath $versionFile) {
        $lines = Get-Content -LiteralPath $versionFile -ErrorAction SilentlyContinue
        foreach ($ln in $lines) {
            $t = $ln.Trim()
            if ($t.Length -gt 0) { $current = $t; break }
        }
    }

    if (-not (Test-UpgradeAvailable -Current $current -Latest $tag)) {
        if ($current -eq $tag) {
            Write-Host "[OK] Already at $tag."
        } else {
            Write-Host "[OK] Already at $current (latest release: $tag). No upgrade needed."
        }
        exit 0
    }

    Write-Host "[INFO] Upgrading from $current to $tag"
    if (-not $Yes) {
        $resp = Read-Host 'Proceed? [y/N]'
        if ($resp -ne 'y' -and $resp -ne 'Y') {
            Write-Host '[INFO] Update cancelled.'
            exit 0
        }
    }

    $stagingName = 'gw2_raids_update_' + [System.IO.Path]::GetRandomFileName()
    $staging = Join-Path $env:TEMP $stagingName
    New-Item -ItemType Directory -Path $staging -Force | Out-Null
    $zipPath = Join-Path $staging 'release.zip'
    $extractDir = Join-Path $staging 'extracted'
    New-Item -ItemType Directory -Path $extractDir -Force | Out-Null

    Write-Host "[INFO] Downloading $zipUrl ..."
    $prevProgress = $ProgressPreference
    $ProgressPreference = 'SilentlyContinue'
    try {
        Invoke-WebRequest -Uri $zipUrl -Headers $headers -OutFile $zipPath -TimeoutSec 120 -ErrorAction Stop
    }
    finally {
        $ProgressPreference = $prevProgress
    }

    Write-Host "[INFO] Extracting..."
    Expand-Archive -LiteralPath $zipPath -DestinationPath $extractDir -Force

    $topDirs = @(Get-ChildItem -LiteralPath $extractDir -Directory)
    if ($topDirs.Count -ne 1) {
        Write-Host "[ERROR] Unexpected zip structure: expected 1 top-level folder, got $($topDirs.Count)."
        exit 1
    }
    $sourceRoot = $topDirs[0].FullName

    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'process_logs.bat'))) {
        Write-Host "[ERROR] Release zip missing process_logs.bat. Aborting."
        exit 1
    }
    if (-not (Test-Path -LiteralPath (Join-Path $sourceRoot 'VERSION'))) {
        Write-Host "[ERROR] Release zip missing VERSION. Aborting."
        exit 1
    }

    Write-Host "[INFO] Replacing bundled subtrees..."
    foreach ($sub in $WholesaleReplace) {
        $target = Join-Path $InstallRoot $sub
        if (Test-Path -LiteralPath $target) {
            Remove-Item -LiteralPath $target -Recurse -Force -ErrorAction Stop
        }
    }

    Write-Host "[INFO] Copying release files into install..."
    $sourceRootFull = (Resolve-Path -LiteralPath $sourceRoot).Path.TrimEnd('\', '/')
    $sourceRootPrefix = $sourceRootFull + '\'

    Get-ChildItem -LiteralPath $sourceRootFull -Recurse -File | ForEach-Object {
        $full = $_.FullName
        if ($full.StartsWith($sourceRootPrefix, [System.StringComparison]::OrdinalIgnoreCase)) {
            $rel = $full.Substring($sourceRootPrefix.Length)
        } else {
            $rel = $_.Name
        }
        if (Test-PreservePath $rel) { return }

        $destPath = Join-Path $InstallRoot $rel
        $destDir = Split-Path -Parent $destPath
        if ($destDir -and -not (Test-Path -LiteralPath $destDir)) {
            New-Item -ItemType Directory -Path $destDir -Force | Out-Null
        }
        Copy-Item -LiteralPath $full -Destination $destPath -Force
    }

    foreach ($regen in @('Resources\Config\EliteInsights.conf', 'Resources\Config\top_stats_config.ini')) {
        $p = Join-Path $InstallRoot $regen
        if (Test-Path -LiteralPath $p) {
            Remove-Item -LiteralPath $p -Force -ErrorAction SilentlyContinue
        }
    }

    Set-Content -LiteralPath $versionFile -Value $tag -Encoding ASCII
    Write-Host "[INFO] VERSION set to $tag."

    Write-Host ""
    Write-Host "[OK] Updated to $tag. Run process_logs.bat to rebuild EI and regenerate configs."
    exit 0
}
catch {
    Write-Host "[ERROR] Update failed: $($_.Exception.Message)"
    exit 1
}
finally {
    if ($staging -and (Test-Path -LiteralPath $staging)) {
        try { Remove-Item -LiteralPath $staging -Recurse -Force -ErrorAction SilentlyContinue } catch { }
    }
}
