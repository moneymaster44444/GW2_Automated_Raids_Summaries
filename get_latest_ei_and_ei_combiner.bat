@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem =========================================================
rem Pull vendored 3rd-party repos (git subtrees) at pinned refs
rem Reads: 3rd_party_repo_version.lock (JSON)
rem Requires: git + PowerShell (Windows PowerShell 5.1+ or PowerShell 7+)
rem =========================================================

set "LOCK=3rd_party_repo_version.lock"

if not exist "%LOCK%" (
  echo [ERROR] Lock file not found: %LOCK%
  exit /b 1
)

rem --- Verify git exists ---
where git >nul 2>&1
if errorlevel 1 (
  echo [ERROR] 'git' not found in PATH.
  exit /b 1
)

rem --- Prefer pwsh (PS7) if available; otherwise fall back to Windows PowerShell ---
where pwsh >nul 2>&1 && (set "PS=pwsh") || (set "PS=powershell")

%PS% -NoProfile -ExecutionPolicy Bypass ^
  -Command ^
  "$ErrorActionPreference='Stop';" ^
  "Write-Host 'Using lock file: %LOCK%';" ^
  "$json = Get-Content '%LOCK%' -Raw | ConvertFrom-Json;" ^
  "if (-not $json -or -not $json.vendors) { throw 'No vendors found in lock file.' }" ^
  "foreach ($v in $json.vendors) {" ^
  "  if (-not $v.repo -or -not $v.prefix -or -not $v.ref) { throw 'Lock entry missing repo/prefix/ref.' }" ^
  "  $name = if ($v.PSObject.Properties.Name -contains 'name' -and $v.name) { $v.name } else { $v.prefix };" ^
  "  $prefixRaw = [string]$v.prefix; $prefix = $prefixRaw -replace '\\','/';" ^
  "  Write-Host ''; Write-Host ('=== ' + $name + ' ===');" ^
  "  Write-Host ('repo:   ' + $v.repo);" ^
  "  Write-Host ('prefix: ' + $prefix);" ^
  "  Write-Host ('ref:    ' + $v.ref);" ^
  "  $prefixExists = Test-Path -LiteralPath $prefixRaw;" ^
  "  if (-not $prefixExists) {" ^
  "    Write-Host 'Prefix not found; performing initial add...';" ^
  "    $args = @('subtree','add','--prefix', $prefix, $v.repo, $v.ref);" ^
  "    if ($v.squash -eq $true) { $args += '--squash' }" ^
  "    & git @args; if ($LASTEXITCODE -ne 0) { throw ('git add failed for ' + $name) }" ^
  "  } else {" ^
  "    $args = @('subtree','pull','--prefix', $prefix, $v.repo, $v.ref);" ^
  "    if ($v.squash -eq $true) { $args += '--squash' }" ^
  "    & git @args; if ($LASTEXITCODE -ne 0) { throw ('git pull failed for ' + $name) }" ^
  "  }" ^
  "}"


if errorlevel 1 (
  echo.
  echo [ERROR] One or more subtree pulls failed.
  exit /b 1
)

echo.
echo [OK] All third-party subtrees updated according to %LOCK%
exit /b 0
