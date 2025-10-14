@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ==========================================
rem GW2 Automated Raids Summaries - Subtree Updater
rem - Updates vendored subtrees (EI & EI Combiner)
rem ==========================================

rem --- Resolve repo root (this script should live at repo root) ---
set "ROOT=%~dp0"
pushd "%ROOT%" >nul 2>&1

rem --- Tool checks (git only) ---
where git >nul 2>&1 || (echo [ERROR] git not found in PATH. Install Git and retry.& exit /b 1)

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

echo ==========================================
echo Updating subtrees...
echo Repo: %CD%
echo ==========================================
echo.

rem --- Capture HEAD before updates to detect change ---
for /f "usebackq delims=" %%H in (`git rev-parse HEAD`) do set "HEAD_BEFORE=%%H"

rem --- Update Elite Insights subtree ---
echo [1/2] Updating Elite Insights subtree...
git subtree pull --prefix="%EI_PREFIX%" "%EI_REMOTE%" %EI_BRANCH% --squash -m "Update Elite Insights subtree"
if errorlevel 1 (
  echo [WARN] Elite Insights subtree pull reported a non-zero exit code.
) else (
  echo [OK] Elite Insights updated ^(or already up-to-date^).
)
echo.

rem --- Update EI Combiner subtree ---
echo [2/2] Updating EI Combiner subtree...
git subtree pull --prefix="%COMB_PREFIX%" "%COMB_REMOTE%" %COMB_BRANCH% --squash -m "Update EI Combiner subtree"
if errorlevel 1 (
  echo [WARN] EI Combiner subtree pull reported a non-zero exit code.
) else (
  echo [OK] EI Combiner updated ^(or already up-to-date^).
)
echo.

rem --- Detect if HEAD changed ---
for /f "usebackq delims=" %%H in (`git rev-parse HEAD`) do set "HEAD_AFTER=%%H"

echo ==========================================
if /i "%HEAD_BEFORE%"=="%HEAD_AFTER%" (
  echo No subtree changes detected ^(already up-to-date^).
) else (
  echo Subtrees updated locally.[31m Run git push after testing.[0m
)
echo Done.
echo ==========================================

popd
exit /b 0
