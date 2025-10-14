@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ==========================================
rem GW2 Automated Raids Summaries - Maintainer Script
rem - Updates vendored subtrees (EI & EI Combiner)
rem - Builds/publishes Elite Insights CLI
rem ==========================================

rem --- Resolve repo root (this script must live at repo root) ---
set "ROOT=%~dp0"
pushd "%ROOT%" >nul 2>&1

rem --- Checking if git or dotnet SDKs are installed ---
where git >nul 2>&1 || (echo [ERROR] git not found in PATH. Install Git and retry.& exit /b 1)
where dotnet >nul 2>&1 || (echo [ERROR] dotnet SDK not found in PATH. Install .NET SDK and retry.& exit /b 1)

git rev-parse --is-inside-work-tree >nul 2>&1 || (
  echo [ERROR] This folder is not a Git repository. Run inside your repo root.
  exit /b 1
)

rem --- Configurable paths/branches ---
set "EI_PREFIX=Resources/Elite Insights"
set "EI_REMOTE=https://github.com/baaron4/GW2-Elite-Insights-Parser.git"
set "EI_BRANCH=master"

set "COMB_PREFIX=Resources/EI Combiner"
set "COMB_REMOTE=https://github.com/Drevarr/GW2_EI_log_combiner.git"
set "COMB_BRANCH=main"

set "EI_CSPROJ=Resources\Elite Insights\GW2EIParserCLI\GW2EIParserCLI.csproj"
set "PUBLISH_DIR=Resources\Elite Insights\GW2EI.bin\Release\CLI"
set "CLI_EXE=%PUBLISH_DIR%\GW2EIParserCLI.exe"

echo ==========================================
echo Updating subtrees and building EI CLI...
echo Repo: %CD%
echo ==========================================
echo.

rem --- Capture HEAD before updates to detect change ---
for /f "usebackq delims=" %%H in (`git rev-parse HEAD`) do set "HEAD_BEFORE=%%H"

rem --- Update Elite Insights subtree ---
echo [1/3] Updating Elite Insights subtree...
git subtree pull --prefix="%EI_PREFIX%" "%EI_REMOTE%" %EI_BRANCH% --squash -m "Update Elite Insights subtree"
if errorlevel 1 (
  echo [WARN] Elite Insights subtree pull reported a non-zero exit code.
) else (
  echo [OK] Elite Insights updated ^(or already up-to-date^).
)
echo.

rem --- Update EI Combiner subtree ---
echo [2/3] Updating EI Combiner subtree...
git subtree pull --prefix="%COMB_PREFIX%" "%COMB_REMOTE%" %COMB_BRANCH% --squash -m "Update EI Combiner subtree"
if errorlevel 1 (
  echo [WARN] EI Combiner subtree pull reported a non-zero exit code.
) else (
  echo [OK] EI Combiner updated ^(or already up-to-date^).
)
echo.

rem --- Detect if we pulled something new from either EI or EI Combiner  ---
for /f "usebackq delims=" %%H in (`git rev-parse HEAD`) do set "HEAD_AFTER=%%H"
set "SUBTREE_CHANGED=0"
if /i not "%HEAD_BEFORE%"=="%HEAD_AFTER%" set "SUBTREE_CHANGED=1"

rem --- Build/publish EI CLI ---
echo [3/3] Publishing Elite Insights CLI...
if not exist "%EI_CSPROJ%" (
  echo [ERROR] Project not found: %EI_CSPROJ%
  echo         Verify the subtree layout and project path.
  popd
  exit /b 1
)

dotnet publish "%EI_CSPROJ%" -c Release -o "%PUBLISH_DIR%"
if errorlevel 1 (
  echo [ERROR] dotnet publish failed. Check SDK installation and project path.
  popd
  exit /b 1
)

if exist "%CLI_EXE%" (
  echo [OK] Publish complete: "%CLI_EXE%"
) else (
  echo [WARN] Publish finished but expected exe not found:
  echo        %CLI_EXE%
  echo        ^(If the TFM or output name changed upstream, adjust this script.^)
)

echo.
echo ==========================================
if "%SUBTREE_CHANGED%"=="1" (
  echo Subtrees updated locally.[31m Run git push after testing.[0m
) else (
  echo No subtree changes detected ^(already up-to-date^).
)
echo Build/publish step completed.
echo ==========================================

popd
exit /b 0
