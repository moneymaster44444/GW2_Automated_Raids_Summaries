@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ==========================================
rem Process arcDPS logs -> EI JSON -> Drag_and_Drop JSON -> TW5 Auto-Imported HTML
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

rem --- Pre-run: requested cleanup of previous run intermediates ---
echo [CLEANUP] Removing leftover JSONs from previous run...
del /q "%DROP_DIR%\*.json"     >nul 2>&1
del /q "%EI_JSON_DIR%\*.json"  >nul 2>&1

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

rem ==========================================================
rem [4] Auto-import into TiddlyWiki and build single-file HTML
rem ==========================================================

rem --- Inputs for TiddlyWiki stage ---
set "TW_BUILD_DIR=%ROOT%Top_Stats_Html"
set "TW_SHELL=%ROOT%Resources\EI Combiner\Example_Output\Top_Stats_Index.html"
set "AUTO_TID=%ROOT%auto-import.tid"

rem --- Sanity checks ---
where tiddlywiki >nul 2>&1
if errorlevel 1 (
  echo [ERROR] "tiddlywiki" not found on PATH. Did you run: npm install -g tiddlywiki ?
  goto :_fail
)

if not exist "%AUTO_TID%" (
  echo [ERROR] auto-import.tid not found at: %AUTO_TID%
  goto :_fail
)

rem --- Work out the filename we just copied into %DROP_DIR% ---
for %%A in ("%LATEST_JSON%") do set "LATEST_NAME=%%~nxA"
set "LATEST_DROP_JSON=%DROP_DIR%\%LATEST_NAME%"

if not exist "%LATEST_DROP_JSON%" (
  echo [ERROR] Expected Drag_and_Drop JSON not found in Raids_Summaries:
  echo         %LATEST_DROP_JSON%
  goto :_fail
)

rem --- One-time init of the build wiki (first run only) ---
if not exist "%TW_BUILD_DIR%" (
  echo [INFO] Initializing build wiki at: %TW_BUILD_DIR%
  call tiddlywiki "%TW_BUILD_DIR%" --init server || goto :_fail

  if not exist "%TW_SHELL%" (
    echo [ERROR] UI shell not found:
    echo         %TW_SHELL%
    goto :_fail
  )

  echo [INFO] Loading UI shell...
  call tiddlywiki "%TW_BUILD_DIR%" --load "%TW_SHELL%" || goto :_fail
)

rem --- Always (re)import the startup auto-import action (safe & idempotent)
echo [INFO] Refreshing startup auto-import action...
call tiddlywiki "%TW_BUILD_DIR%" --import "%AUTO_TID%" text/plain || goto :_fail

echo [INFO] Injecting latest JSON into $:/data/dragdrop...
call tiddlywiki "%TW_BUILD_DIR%" --import "%LATEST_DROP_JSON%" application/json "$:/data/dragdrop" || goto :_fail

echo [INFO] Building single-file HTML (target: index)...
call tiddlywiki "%TW_BUILD_DIR%" --build index || goto :_fail

set "TW_OUT=%TW_BUILD_DIR%\output\index.html"
if exist "%TW_OUT%" (
  echo [OK] Build complete:
  echo      %TW_OUT%

  rem --- Compute date tag robustly ---
  call :compute_date_tag
  echo [INFO] Using date tag: !DATE_TAG!

  rem --- Final path in Raids_Summaries ---
  set "TW_FINAL=%DROP_DIR%\INC_!DATE_TAG!.html"

  rem --- Copy then delete original (safer than MOVE if anything goes wrong) ---
  copy /y "%TW_OUT%" "!TW_FINAL!" >nul
  if errorlevel 1 (
    echo [WARN] Copy failed; leaving original at:
    echo       %TW_OUT%
  ) else (
    del /q "%TW_OUT%" >nul 2>&1
    if exist "!TW_FINAL!" (
      echo [OK] Final HTML written to:
      echo      !TW_FINAL!
    ) else (
      echo [WARN] Unexpected: final file missing after copy. Check permissions/paths.
    )
  )
) else (
  echo [WARN] Build finished but index.html not found where expected:
  echo       %TW_OUT%
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
echo Raid Summary HTML is at:
if defined TW_FINAL (
  echo   !TW_FINAL!
) else (
  echo   %TW_OUT%
)
echo ==========================================
exit /b 0

:_fail
echo [ERROR] Pipeline aborted. See messages above.
exit /b 1

:_run_ei
rem %~1 = full path to log file
if not exist %~1 goto :eof
set "FOUND_LOG=1"
echo     -> "%~nx1"
"%EI_CLI_EXE%" -c "%EI_CONF%" "%~1"
if errorlevel 1 echo     [WARN] EI returned non-zero for %~nx1
goto :eof

:compute_date_tag
rem --- Helper: compute MM-dd-yy date tag into DATE_TAG ---
set "DATE_TAG="

rem 1) Try Windows PowerShell (prefer 64-bit System32; then Sysnative; then PATH)
for %%P in (
  "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
  "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
  "powershell.exe"
) do (
  if not defined DATE_TAG if exist "%%~fP" (
    for /f "delims=" %%D in ('"%%~fP" -NoProfile -Command "(Get-Date).ToString('MM-dd-yy')" 2^>nul') do (
      set "DATE_TAG=%%D"
    )
  )
)

rem 2) Fallback: WMIC (YYYYMMDDhhmmss.mmm+TZ) -> MM-dd-yy
if not defined DATE_TAG (
  for /f "tokens=2 delims==" %%L in ('wmic os get LocalDateTime /value 2^>nul ^| find "="') do set "LDT=%%L"
  if defined LDT (
    set "DATE_TAG=!LDT:~4,2!-!LDT:~6,2!-!LDT:~2,2!"
  )
)

rem 3) Last resort: parse %DATE% (locale-dependent heuristic)
if not defined DATE_TAG (
  for /f "tokens=1-4 delims=/-. " %%a in ("%DATE%") do (
    set "a=%%a" & set "b=%%b" & set "c=%%c" & set "d=%%d"
  )
  if defined b if defined c if defined d (
    set "DATE_TAG=!b!-!c!-!d:~-2!"
  ) else if defined a if defined b if defined c (
    set "DATE_TAG=!a!-!b!-!c:~-2!"
  )
)

if not defined DATE_TAG set "DATE_TAG=unknown"
exit /b 0
