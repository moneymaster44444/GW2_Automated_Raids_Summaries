@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ==========================================
rem GW2 Automated Raids Summaries - EI CLI Builder
rem - Builds/publishes Elite Insights CLI
rem ==========================================

rem --- Resolve repo root (this script should live at repo root) ---
set "ROOT=%~dp0"
pushd "%ROOT%" >nul 2>&1

rem --- Tool checks (dotnet only) ---
where dotnet >nul 2>&1 || (echo [ERROR] dotnet SDK not found in PATH. Install .NET SDK and retry.& exit /b 1)

rem --- Configurable paths ---
set "EI_CSPROJ=Resources\Elite Insights\GW2EIParserCLI\GW2EIParserCLI.csproj"
set "PUBLISH_DIR=Resources\Elite Insights\GW2EI.bin\Release\CLI"

rem NOTE:
rem If your output exe name differs (e.g., GuildWars2EliteInsights-CLI.exe),
rem either update CLI_EXE below or let the generic *.exe detection print what's produced.
set "CLI_EXE=%PUBLISH_DIR%\GW2EIParserCLI.exe"

echo ==========================================
echo Publishing Elite Insights CLI...
echo Repo: %CD%
echo ==========================================
echo.

if not exist "%EI_CSPROJ%" (
  echo [ERROR] Project not found: %EI_CSPROJ%
  echo         Verify the subtree layout and project path.
  popd
  exit /b 1
)

rem Optional: allow config parameter, default Release (usage: build_ei_cli.bat Debug)
set "BUILD_CFG=%~1"
if "%BUILD_CFG%"=="" set "BUILD_CFG=Release"

echo [1/1] dotnet publish -c %BUILD_CFG% -o "%PUBLISH_DIR%"
dotnet publish "%EI_CSPROJ%" -c %BUILD_CFG% -o "%PUBLISH_DIR%"
if errorlevel 1 (
  echo [ERROR] dotnet publish failed. Check SDK installation and project path.
  popd
  exit /b 1
)

if exist "%CLI_EXE%" (
  echo [OK] Publish complete: "%CLI_EXE%"
) else (
  echo [INFO] Publish complete, listing executables found in:
  echo        "%PUBLISH_DIR%"
  dir /b "%PUBLISH_DIR%\*.exe" 2>nul
  if errorlevel 1 (
    echo [WARN] No .exe found in publish output. If TFM/output name changed upstream, update CLI_EXE in this script.
  )
)

echo.
echo ==========================================
echo Build/publish step completed.
echo ==========================================

popd
exit /b 0
