@echo off
setlocal EnableExtensions

rem ==========================================
rem Process arcDPS logs -> EI JSON -> Drag_and_Drop JSON
rem ==========================================

rem --- Resolve repo root (this script must live at repo root) ---
set "ROOT=%~dp0"

rem --- Canonical paths (aligned with your config scripts) ---
set "LOGS_DIR=%ROOT%Raid_Logs"
set "EI_JSON_DIR=%ROOT%Raids_Summaries\EI_json_output"
set "DROP_DIR=%ROOT%Raids_Summaries"

rem --- Config files created by establish_config_files.bat ---
set "EI_CONF=%ROOT%Resources\Config\EliteInsights.conf"
set "COMBINER_INI=%ROOT%Resources\Config\top_stats_config.ini"

rem --- Elite Insights CLI publish/output dir (target of dotnet publish) ---
set "EI_CLI_DIR=%ROOT%Resources\Elite Insights\GW2EI.bin\Release\CLI"

rem --- Possible EI CLI exe names ---
set "EI_EXE_NAME1=GW2EIParserCLI.exe"
set "EI_EXE_NAME2=GuildWars2EliteInsights-CLI.exe"

rem --- EI CLI project (for fallback publish) ---
set "EI_CSPROJ=%ROOT%Resources\Elite Insights\GW2EIParserCLI\GW2EIParserCLI.csproj"

rem --- EI Combiner (Python script) ---
set "PYTHON_EXE=python"
set "COMBINER_PY=%ROOT%Resources\EI Combiner\tw5_top_stats.py"

echo ==========================================
echo Running GW2 log processing pipeline...
echo Repo: %ROOT%
echo Logs: %LOGS_DIR%
echo Out : %EI_JSON_DIR%
echo ==========================================
echo.

rem --- Pre-run: make sure folders exist ---
if not exist "%LOGS_DIR%"    mkdir "%LOGS_DIR%"
if not exist "%EI_JSON_DIR%" mkdir "%EI_JSON_DIR%"
if not exist "%DROP_DIR%"    mkdir "%DROP_DIR%"

rem --- Pre-run: DELETE any stray arcDPS logs at repo root (do not move) ---
del /q "%ROOT%*.zevtc" >nul 2>&1
del /q "%ROOT%*.evtc"  >nul 2>&1

rem --- Ensure config files exist ---
if not exist "%EI_CONF%" (
  echo [ERROR] EliteInsights.conf not found: %EI_CONF%
  echo         Run establish_config_files.bat first.
  goto :_fail
)
if not exist "%COMBINER_INI%" (
  echo [ERROR] top_stats_config.ini not found: %COMBINER_INI%
  echo         Run establish_config_files.bat first.
  goto :_fail
)

rem --- Resolve EI CLI path (try common names, then search, then build) ---
set "EI_CLI_EXE="
if exist "%EI_CLI_DIR%\%EI_EXE_NAME1%" set "EI_CLI_EXE=%EI_CLI_DIR%\%EI_EXE_NAME1%"
if not defined EI_CLI_EXE if exist "%EI_CLI_DIR%\%EI_EXE_NAME2%" set "EI_CLI_EXE=%EI_CLI_DIR%\%EI_EXE_NAME2%"

if not defined EI_CLI_EXE (
  for /f "delims=" %%P in ('dir /b /s "%ROOT%Resources\Elite Insights\*CLI*.exe" 2^>nul') do (
    if not defined EI_CLI_EXE set "EI_CLI_EXE=%%~fP"
  )
)

if not defined EI_CLI_EXE (
  if exist "%EI_CSPROJ%" (
    echo [INFO] EI CLI not found. Attempting to publish...
    dotnet publish "%EI_CSPROJ%" -c Release -o "%EI_CLI_DIR%"
    if exist "%EI_CLI_DIR%\%EI_EXE_NAME1%" set "EI_CLI_EXE=%EI_CLI_DIR%\%EI_EXE_NAME1%"
    if not defined EI_CLI_EXE if exist "%EI_CLI_DIR%\%EI_EXE_NAME2%" set "EI_CLI_EXE=%EI_CLI_DIR%\%EI_EXE_NAME2%"
  )
)

if not defined EI_CLI_EXE (
  echo [ERROR] EI CLI executable not found.
  goto :_fail
)

echo [OK] Using EI CLI:
echo      "%EI_CLI_EXE%"
echo.

rem --- [1/3] Parse logs with EI ---
echo [1/3] Parsing arcDPS logs with Elite Insights...
set "FOUND_LOG=0"

for %%F in ("%LOGS_DIR%\*.zevtc") do call :_run_ei "%%~fF"
for %%F in ("%LOGS_DIR%\*.evtc")  do call :_run_ei "%%~fF"

if not "%FOUND_LOG%"=="1" (
  echo [INFO] No .zevtc or .evtc files found in:
  echo        %LOGS_DIR%
  echo        Put logs there and re-run.
  goto :_post_cleanup_success
)

echo [OK] EI parse step complete.
echo.

rem --- [2/3] Run EI Combiner ---
echo [2/3] Running EI Combiner...
if not exist "%COMBINER_PY%" (
  echo [ERROR] Combiner script not found: %COMBINER_PY%
  goto :_fail
)

"%PYTHON_EXE%" "%COMBINER_PY%" -i "%EI_JSON_DIR%" -c "%COMBINER_INI%"
if errorlevel 1 (
  echo [WARN] EI Combiner returned non-zero; check Python/deps/config.
) else (
  echo [OK] EI Combiner step complete.
)
echo.

rem --- [3/3] Find newest Drag_and_Drop JSON in EI_JSON_DIR only ---
echo [3/3] Finalizing Drag_and_Drop JSON...
set "LATEST_JSON="

for /f "delims=" %%J in ('dir /b /a:-d /o:-d "%EI_JSON_DIR%\Drag_and_Drop_Log_Summary_*.json" 2^>nul') do (
  if not defined LATEST_JSON set "LATEST_JSON=%EI_JSON_DIR%\%%~J"
)

if not defined LATEST_JSON (
  echo [WARN] No Drag_and_Drop JSON found in:
  echo        %EI_JSON_DIR%
  goto :_post_cleanup_success
)

rem --- Copy newest into Raids_Summaries ---
copy /y "%LATEST_JSON%" "%DROP_DIR%" >nul
if errorlevel 1 (
  echo [WARN] Could not copy JSON to Raids_Summaries.
  echo       From: %LATEST_JSON%
  echo       To  : %DROP_DIR%
) else (
  echo [OK] Copied latest Drag_and_Drop JSON to Raids_Summaries.
  echo       From: "%LATEST_JSON%"
  echo       To  : "%DROP_DIR%"
  rem Remove all Drag_and_Drop files from EI_JSON_DIR (we only keep the copy in DROP_DIR)
  del /q "%EI_JSON_DIR%\Drag_and_Drop_Log_Summary_*.json" >nul 2>&1
)

:_post_cleanup_success
rem --- Post-run: DELETE any stray arcDPS logs at repo root (do not move) ---
del /q "%ROOT%*.zevtc" >nul 2>&1
del /q "%ROOT%*.evtc"  >nul 2>&1

echo.
echo ==========================================
echo Pipeline complete.
echo Drag_and_Drop JSON available under:
echo   %DROP_DIR%
echo ==========================================
exit /b 0

:_fail
exit /b 1

:_run_ei
rem %~1 = full path to log file
if not exist %~1 goto :eof
set "FOUND_LOG=1"
echo     -> "%~nx1"
"%EI_CLI_EXE%" -c "%EI_CONF%" "%~1" >nul 2>&1
if errorlevel 1 echo     [WARN] EI returned non-zero for %~nx1
goto :eof
