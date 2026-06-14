# Creates a Windows shortcut that launches the GW2 Raid Summaries GUI and shows
# the guild logo as its icon - so it looks and behaves like an app instead of a
# .bat file. The shortcut points straight at the built executable, so launching
# it opens no console window.
#
# Usage:
#   powershell -ExecutionPolicy Bypass -File Scripts\Create-Launcher-Shortcut.ps1
#   ...add -Desktop to also drop a copy on your Desktop.
#
# The generated .lnk stores absolute paths, so it is per-machine and gitignored.

[CmdletBinding()]
param(
    [switch]$Desktop
)

$ErrorActionPreference = 'Stop'

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { Split-Path -Parent $MyInvocation.MyCommand.Path }
$repoRoot = Split-Path -Parent $scriptDir
$proj = Join-Path $repoRoot 'Gui\GW2RaidsGui.csproj'
$exe = Join-Path $repoRoot 'Gui\bin\Release\net8.0-windows\GW2RaidsGui.exe'
$icon = Join-Path $repoRoot 'Gui\app.ico'
$shortcutName = 'GW2 Raid Summaries.lnk'

# Build once if the executable isn't there yet.
if (-not (Test-Path -LiteralPath $exe)) {
    Write-Host '[SETUP] Building the GUI so the shortcut has something to point at...'
    & dotnet build $proj -c Release --nologo -v minimal
    if ($LASTEXITCODE -ne 0) { throw 'Build failed; cannot create the shortcut.' }
}
if (-not (Test-Path -LiteralPath $exe)) { throw "Executable not found after build: $exe" }

function New-LauncherShortcut([string]$Path) {
    $shell = New-Object -ComObject WScript.Shell
    $lnk = $shell.CreateShortcut($Path)
    $lnk.TargetPath = $exe
    $lnk.Arguments = '"' + $repoRoot + '"'   # repo root, so the app finds config / logs
    $lnk.WorkingDirectory = $repoRoot
    $lnk.IconLocation = "$icon,0"
    $lnk.Description = 'Launch the GW2 Raid Summaries GUI'
    $lnk.Save()
    Write-Host "[OK] Created shortcut: $Path"
}

New-LauncherShortcut (Join-Path $repoRoot $shortcutName)

if ($Desktop) {
    $desktopDir = [Environment]::GetFolderPath('Desktop')
    New-LauncherShortcut (Join-Path $desktopDir $shortcutName)
}
