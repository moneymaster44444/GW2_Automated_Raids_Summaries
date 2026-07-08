@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ==========================================
rem Process arcDPS logs -> EI JSON -> Drag_and_Drop JSON -> TW5 Auto-Imported HTML
rem ==========================================

rem --- Resolve repo root (this script must live at repo root) ---
set "ROOT=%~dp0"

rem --- Run mode (optional first arg): --manual | --scheduled ---
rem   (no arg)      Legacy behavior: auto-copy from LOG_SOURCE_DIR if it is set
rem                 in config.txt, otherwise process whatever is in Raid_Logs.
rem   --manual / -m Skip the auto-copy; process the current Raid_Logs contents.
rem   --scheduled   Force the raid-window auto-copy from LOG_SOURCE_DIR
rem        / -s     (requires LOG_SOURCE_DIR to be set), then process.
set "RUN_MODE="
if /i "%~1"=="--manual"    set "RUN_MODE=manual"
if /i "%~1"=="--scheduled" set "RUN_MODE=scheduled"
if /i "%~1"=="--setup"     set "RUN_MODE=setup"
if /i "%~1"=="-m"          set "RUN_MODE=manual"
if /i "%~1"=="-s"          set "RUN_MODE=scheduled"
if /i "%~1"=="--help"      set "RUN_MODE=help"
if /i "%~1"=="-h"          set "RUN_MODE=help"
if /i "%~1"=="/?"          set "RUN_MODE=help"
if "%RUN_MODE%"=="help" (
  call :print_usage
  exit /b 0
)
if not "%~1"=="" if not defined RUN_MODE (
  echo [ERROR] Unknown argument: %~1
  call :print_usage
  exit /b 1
)

rem --- Canonical paths ---
set "LOGS_DIR=%ROOT%Raid_Logs"
set "EI_JSON_DIR=%ROOT%Raids_Summaries\EI_json_output"
set "DROP_DIR=%ROOT%Raids_Summaries"
set "SCRIPTS_DIR=%ROOT%Scripts"

rem --- Config files created by establish_config_files.bat ---
set "EI_CONF=%ROOT%Resources\Config\EliteInsights.conf"
set "COMBINER_INI=%ROOT%Resources\Config\top_stats_config.ini"

rem --- Elite Insights CLI ---
set "EI_CLI_DIR=%ROOT%Resources\Elite Insights\GW2EI.bin\Release\CLI"
set "EI_EXE_NAME1=GW2EIParserCLI.exe"
set "EI_EXE_NAME2=GuildWars2EliteInsights-CLI.exe"
set "EI_CSPROJ=%ROOT%Resources\Elite Insights\GW2EIParserCLI\GW2EIParserCLI.csproj"

rem --- EI Combiner (Python) ---
set "PYTHON_EXE=python"
set "COMBINER_PY=%ROOT%Resources\EI Combiner\tw5_top_stats.py"
set "REQUIREMENTS_TXT=%ROOT%requirements.txt"

rem --- User config (NAME=VALUE pairs; lines starting with # are ignored) ---
set "CONFIG_FILE=%ROOT%config.txt"
set "SAMPLE_CONFIG_FILE=%ROOT%sample.config.txt"
if not exist "%CONFIG_FILE%" (
  if exist "%SAMPLE_CONFIG_FILE%" (
    echo [SETUP] Creating config.txt from sample.config.txt...
    copy /y "%SAMPLE_CONFIG_FILE%" "%CONFIG_FILE%" >nul
  )
) else (
  if exist "%SAMPLE_CONFIG_FILE%" (
    rem Migrate: append any new fields present in sample.config.txt but
    rem missing from the user's config.txt. No-op when they're in sync.
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPTS_DIR%\Sync-UserConfig.ps1" ^
      -SamplePath "%SAMPLE_CONFIG_FILE%" -UserPath "%CONFIG_FILE%"
  )
)
set "GUILD_TAG="
set "DISCORD_WEBHOOK_URL="
set "MAX_PARALLEL_EI="
set "LOG_SOURCE_DIR="
set "MIN_LOG_SIZE_KB="
set "RAID_LOGS_GRACE_HOURS="
call :load_config
if not defined GUILD_TAG set "GUILD_TAG=OnLY"
if not defined MAX_PARALLEL_EI set "MAX_PARALLEL_EI=0"
if not defined MIN_LOG_SIZE_KB set "MIN_LOG_SIZE_KB=900"

set "DISCORD_POSTED=0"
set "DISCORD_REASON="
set "DISCORD_POSTED_NAME="

rem ==========================================
rem First-time setup: ensure configs and EI CLI exist
rem ==========================================

set "NEED_CONFIG_SETUP=0"
if not exist "%EI_CONF%"      set "NEED_CONFIG_SETUP=1"
if not exist "%COMBINER_INI%" set "NEED_CONFIG_SETUP=1"

if "%NEED_CONFIG_SETUP%"=="1" (
  if exist "%ROOT%establish_config_files.bat" (
    echo [SETUP] Running establish_config_files.bat to create config files...
    call "%ROOT%establish_config_files.bat"
    if errorlevel 1 (
      echo [ERROR] establish_config_files.bat failed.
      goto :fail
    )
  ) else (
    echo [ERROR] establish_config_files.bat not found at:
    echo         %ROOT%establish_config_files.bat
    goto :fail
  )
)

if not exist "%EI_CONF%" (
  echo [ERROR] EliteInsights.conf not found after setup: %EI_CONF%
  goto :fail
)
if not exist "%COMBINER_INI%" (
  echo [ERROR] top_stats_config.ini not found after setup: %COMBINER_INI%
  goto :fail
)

set "NEED_EI_BUILD=0"
if not exist "%EI_CLI_DIR%\%EI_EXE_NAME1%" (
  if not exist "%EI_CLI_DIR%\%EI_EXE_NAME2%" set "NEED_EI_BUILD=1"
)

if "%NEED_EI_BUILD%"=="1" (
  if exist "%ROOT%build_elite_insights.bat" (
    echo [SETUP] Running build_elite_insights.bat to build EI CLI...
    call "%ROOT%build_elite_insights.bat"
    if errorlevel 1 (
      echo [ERROR] build_elite_insights.bat failed.
      goto :fail
    )
  ) else (
    echo [ERROR] build_elite_insights.bat not found at:
    echo         %ROOT%build_elite_insights.bat
    goto :fail
  )
)

rem ==========================================
rem Ensure Python deps for EI Combiner (idempotent)
rem   Installs into the SAME interpreter that runs the combiner (%PYTHON_EXE%),
rem   then RE-VERIFIES the imports. pip can exit 0 yet install into a different
rem   site than %PYTHON_EXE% imports from, so a plain exit code is not trusted.
rem   If the deps still cannot be imported after installing, this is a hard
rem   error (the combiner cannot run without them) rather than a silent warning
rem   that leaves a cryptic ModuleNotFoundError three steps later.
rem ==========================================

echo [SETUP] Checking Python dependencies for EI Combiner...
call :check_python_deps
if "!PY_DEPS_OK!"=="1" (
  echo [OK] Python dependencies already present.
  goto :py_deps_done
)

echo [SETUP] One or more packages missing; installing...

rem Make sure pip is available for THIS interpreter before using it.
"%PYTHON_EXE%" -m pip --version >nul 2>&1
if errorlevel 1 (
  echo [SETUP] pip not available for the target Python; bootstrapping with ensurepip...
  "%PYTHON_EXE%" -m ensurepip --upgrade >nul 2>&1
)

if exist "%REQUIREMENTS_TXT%" (
  "%PYTHON_EXE%" -m pip install --disable-pip-version-check -r "%REQUIREMENTS_TXT%"
) else (
  "%PYTHON_EXE%" -m pip install --disable-pip-version-check requests xlsxwriter glicko2
)

rem Re-verify against the interpreter that will actually run the combiner.
call :check_python_deps
if "!PY_DEPS_OK!"=="1" (
  echo [OK] Python dependencies installed.
  goto :py_deps_done
)

echo.
echo [ERROR] Python dependencies are still missing after attempting installation.
echo         The EI Combiner cannot run without: requests, xlsxwriter, glicko2
set "PY_RESOLVED="
for /f "usebackq delims=" %%E in (`"%PYTHON_EXE%" -c "import sys;print(sys.executable)" 2^>nul`) do set "PY_RESOLVED=%%E"
if not defined PY_RESOLVED set "PY_RESOLVED=%PYTHON_EXE%"
echo         Python used: !PY_RESOLVED!
echo         Install them manually into THAT interpreter, then re-run:
echo             "!PY_RESOLVED!" -m pip install -r "%REQUIREMENTS_TXT%"
echo         If you have more than one Python installed, make sure the first
echo         "python" on your PATH is the one shown above.
goto :fail

:py_deps_done

rem ==========================================
rem Ensure TiddlyWiki CLI (idempotent)
rem ==========================================

echo [SETUP] Checking TiddlyWiki CLI...
where tiddlywiki >nul 2>&1
if errorlevel 1 (
  echo [SETUP] tiddlywiki not found; installing globally via npm...
  where npm >nul 2>&1
  if errorlevel 1 (
    echo [WARN] npm not found on PATH. Install Node.js from https://nodejs.org and re-run.
  ) else (
    call npm install -g tiddlywiki
    where tiddlywiki >nul 2>&1
    if errorlevel 1 (
      echo [WARN] tiddlywiki still not found after install.
      echo        Open a NEW terminal so PATH refreshes, then re-run.
    ) else (
      echo [OK] tiddlywiki installed.
    )
  )
) else (
  echo [OK] tiddlywiki already present.
)

rem ==========================================
rem First-time setup only (GUI --setup): everything above this point has
rem ensured configs, the EI CLI, Python deps, and TiddlyWiki. Exit before
rem touching any logs.
rem ==========================================
if /i "%RUN_MODE%"=="setup" (
  echo.
  echo [SETUP] First-time setup complete.
  exit /b 0
)

rem ==========================================
rem Normal pipeline start
rem ==========================================

echo ==========================================
echo Running GW2 log processing pipeline...
echo Repo: %ROOT%
echo Logs: %LOGS_DIR%
echo Out : %EI_JSON_DIR%
echo ==========================================
echo.

if not exist "%LOGS_DIR%"    mkdir "%LOGS_DIR%"
if not exist "%EI_JSON_DIR%" mkdir "%EI_JSON_DIR%"
if not exist "%DROP_DIR%"    mkdir "%DROP_DIR%"

set "RAID_DATE_OVERRIDE="

rem --- Decide whether to run the scheduled raid-window auto-copy step ---
set "DO_AUTOCOPY=0"
if /i "%RUN_MODE%"=="scheduled" (
  if not defined LOG_SOURCE_DIR (
    echo [ERROR] --scheduled requires LOG_SOURCE_DIR to be set in config.txt.
    echo         Set LOG_SOURCE_DIR to your arcDPS log folder, or use --manual
    echo         to process the logs currently in Raid_Logs.
    goto :fail
  )
  set "DO_AUTOCOPY=1"
) else if /i "%RUN_MODE%"=="manual" (
  rem Manual mode: never auto-copy; process whatever is in Raid_Logs.
  set "DO_AUTOCOPY=0"
) else (
  rem Default/legacy: auto-copy only when LOG_SOURCE_DIR is configured.
  if defined LOG_SOURCE_DIR set "DO_AUTOCOPY=1"
)

if "%DO_AUTOCOPY%"=="1" (
  echo [INFO] Resolving active raid window from config...
  set "RW_TMP=%TEMP%\raidwindow_%RANDOM%_%RANDOM%.out"
  powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPTS_DIR%\Resolve-RaidWindow.ps1" ^
    -ConfigFile "%CONFIG_FILE%" > "!RW_TMP!"
  set "RW_STATUS="
  set "RW_START="
  set "RW_END="
  set "RW_RAID_DATE="
  set "RW_REASON="
  set "RW_DAY_NAME="
  for /f "usebackq tokens=1,* delims==" %%A in ("!RW_TMP!") do (
    if /i "%%A"=="STATUS"    set "RW_STATUS=%%B"
    if /i "%%A"=="START"     set "RW_START=%%B"
    if /i "%%A"=="END"       set "RW_END=%%B"
    if /i "%%A"=="RAID_DATE" set "RW_RAID_DATE=%%B"
    if /i "%%A"=="DAY_NAME"  set "RW_DAY_NAME=%%B"
    if /i "%%A"=="REASON"    set "RW_REASON=%%B"
    if /i "%%A"=="REMARK"    echo [WARN] %%B
  )
  del /q "!RW_TMP!" >nul 2>&1
  if /i "!RW_STATUS!"=="ok" (
    echo [INFO] Active raid: !RW_DAY_NAME! ^(!RW_RAID_DATE!^) - !RW_REASON!
    echo [INFO] Copy window: !RW_START!  -^>  !RW_END!
    set "RAID_DATE_OVERRIDE=!RW_RAID_DATE!"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPTS_DIR%\Copy-RaidLogs.ps1" ^
      -SourceDir "!LOG_SOURCE_DIR!" -DestDir "%LOGS_DIR%" ^
      -WindowStart "!RW_START!" -WindowEnd "!RW_END!" -MinSizeKB %MIN_LOG_SIZE_KB%
  ) else (
    echo [INFO] No raid logs to copy: !RW_REASON!
    rem Clear Raid_Logs so we don't reprocess stale files from a prior run.
    del /q "%LOGS_DIR%\*.zevtc" >nul 2>&1
    del /q "%LOGS_DIR%\*.evtc"  >nul 2>&1
  )
)

del /q "%ROOT%*.zevtc" >nul 2>&1
del /q "%ROOT%*.evtc"  >nul 2>&1

echo [CLEANUP] Removing leftover intermediates from previous run...
del /q "%DROP_DIR%\*.json"       >nul 2>&1
del /q "%EI_JSON_DIR%\*.json"    >nul 2>&1
del /q "%EI_JSON_DIR%\*.json.gz" >nul 2>&1
del /q "%EI_JSON_DIR%\*.log"     >nul 2>&1

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
  goto :fail
)

echo [OK] Using EI CLI:
echo      "%EI_CLI_EXE%"
echo.

echo [1/3] Parsing arcDPS logs with Elite Insights (parallel)...
set "EI_PARSE_RC=0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPTS_DIR%\Invoke-EliteInsightsParallel.ps1" ^
  -EiExe "%EI_CLI_EXE%" -EiConf "%EI_CONF%" -LogsDir "%LOGS_DIR%" -MaxParallel %MAX_PARALLEL_EI%
set "EI_PARSE_RC=%ERRORLEVEL%"

if "%EI_PARSE_RC%"=="10" (
  echo [INFO] No .zevtc or .evtc files found in:
  echo        %LOGS_DIR%
  echo        Put logs there and re-run.
  goto :post_cleanup_success
)
if "%EI_PARSE_RC%"=="2" (
  echo [ERROR] Parallel EI runner reported a configuration problem.
  goto :fail
)
if not "%EI_PARSE_RC%"=="0" (
  echo [WARN] One or more EI invocations failed; continuing with whatever JSON was produced.
)

echo [OK] EI parse step complete.
echo.

echo [2/3] Running EI Combiner...
if not exist "%COMBINER_PY%" (
  echo [ERROR] Combiner script not found: %COMBINER_PY%
  goto :fail
)

"%PYTHON_EXE%" "%COMBINER_PY%" -i "%EI_JSON_DIR%" -c "%COMBINER_INI%"
if errorlevel 1 (
  echo [WARN] EI Combiner returned non-zero; check Python/deps/config.
) else (
  echo [OK] EI Combiner step complete.
)
echo.

echo [3/3] Finalizing Drag_and_Drop JSON...
set "LATEST_JSON="
for /f "delims=" %%J in ('dir /b /a:-d /o:-d "%EI_JSON_DIR%\Drag_and_Drop_Log_Summary_*.json" 2^>nul') do (
  if not defined LATEST_JSON set "LATEST_JSON=%EI_JSON_DIR%\%%~J"
)

if not defined LATEST_JSON (
  echo [WARN] No Drag_and_Drop JSON found in:
  echo        %EI_JSON_DIR%
  goto :post_cleanup_success
)

copy /y "%LATEST_JSON%" "%DROP_DIR%" >nul
if errorlevel 1 (
  echo [WARN] Could not copy JSON to Raids_Summaries.
  echo       From: %LATEST_JSON%
  echo       To  : %DROP_DIR%
) else (
  echo [OK] Copied latest Drag_and_Drop JSON to Raids_Summaries.
  echo       From: "%LATEST_JSON%"
  echo       To  : "%DROP_DIR%"
  del /q "%EI_JSON_DIR%\Drag_and_Drop_Log_Summary_*.json" >nul 2>&1
)

rem ==========================================
rem [4] Auto-import into TiddlyWiki and build single-file HTML
rem ==========================================

set "TW_BUILD_DIR=%ROOT%Top_Stats_Html"
if exist "%TW_BUILD_DIR%" (
  echo [CLEANUP] Removing previous TiddlyWiki output at: "%TW_BUILD_DIR%"
  rmdir /s /q "%TW_BUILD_DIR%" 2>nul
  if exist "%TW_BUILD_DIR%" (
    echo [WARN] First attempt to remove "%TW_BUILD_DIR%" failed ^(files in use?^) Retrying...
    attrib -r -s -h "%TW_BUILD_DIR%\*" /s /d >nul 2>&1
    ping -n 2 127.0.0.1 >nul
    rmdir /s /q "%TW_BUILD_DIR%"
    if exist "%TW_BUILD_DIR%" (
      echo [ERROR] Could not remove "%TW_BUILD_DIR%". Close any open files in that folder and re-run.
      goto :fail
    )
  )
)

set "TW_SHELL=%ROOT%Resources\EI Combiner\Example_Output\Top_Stats_Index.html"
set "AUTO_TID=%ROOT%auto-import.tid"

where tiddlywiki >nul 2>&1
if errorlevel 1 (
  echo [ERROR] "tiddlywiki" not found on PATH.
  echo         Auto-install may have failed, or PATH has not refreshed yet.
  echo         Run: npm install -g tiddlywiki  then open a new terminal and re-run.
  goto :fail
)

if not exist "%AUTO_TID%" (
  echo [ERROR] auto-import.tid not found at: %AUTO_TID%
  goto :fail
)

for %%A in ("%LATEST_JSON%") do set "LATEST_NAME=%%~nxA"
set "LATEST_DROP_JSON=%DROP_DIR%\%LATEST_NAME%"

if not exist "%LATEST_DROP_JSON%" (
  echo [ERROR] Expected Drag_and_Drop JSON not found in Raids_Summaries:
  echo         %LATEST_DROP_JSON%
  goto :fail
)

if not exist "%TW_BUILD_DIR%" (
  echo [INFO] Initializing build wiki at: %TW_BUILD_DIR%
  call tiddlywiki "%TW_BUILD_DIR%" --init server || goto :fail

  if not exist "%TW_SHELL%" (
    echo [ERROR] UI shell not found:
    echo         %TW_SHELL%
    goto :fail
  )

  echo [INFO] Loading UI shell...
  call tiddlywiki "%TW_BUILD_DIR%" --load "%TW_SHELL%" || goto :fail
)

echo [INFO] Refreshing startup auto-import action...
call tiddlywiki "%TW_BUILD_DIR%" --import "%AUTO_TID%" text/plain || goto :fail

echo [INFO] Injecting latest JSON into $:/data/dragdrop...
call tiddlywiki "%TW_BUILD_DIR%" --import "%LATEST_DROP_JSON%" application/json "$:/data/dragdrop" || goto :fail

echo [INFO] Building single-file HTML (target: index)...
call tiddlywiki "%TW_BUILD_DIR%" --build index || goto :fail

set "TW_OUT=%TW_BUILD_DIR%\output\index.html"
if exist "%TW_OUT%" (
  echo [OK] Build complete:
  echo      %TW_OUT%

  set "DATE_TAG="
  if defined RAID_DATE_OVERRIDE set "DATE_TAG=!RAID_DATE_OVERRIDE!"
  if not defined DATE_TAG (
    for /f "usebackq delims=" %%D in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPTS_DIR%\Get-DateTag.ps1"`) do set "DATE_TAG=%%D"
  )
  if not defined DATE_TAG set "DATE_TAG=unknown"
  echo [INFO] Using date tag: !DATE_TAG!

  set "TW_FINAL=%DROP_DIR%\!GUILD_TAG!_!DATE_TAG!.html"

  copy /y "%TW_OUT%" "!TW_FINAL!" >nul
  if errorlevel 1 (
    echo [WARN] Copy failed; leaving original at:
    echo       %TW_OUT%
  ) else (
    del /q "%TW_OUT%" >nul 2>&1
    if exist "!TW_FINAL!" (
      echo [OK] Final HTML written to:
      echo      !TW_FINAL!

      call :notify_discord "!TW_FINAL!" "!DISCORD_WEBHOOK_URL!"

      if exist "%TW_BUILD_DIR%" (
        echo [CLEANUP] Removing intermediate TiddlyWiki build folder...
        rmdir /s /q "%TW_BUILD_DIR%" >nul 2>&1
        if exist "%TW_BUILD_DIR%" (
          echo [WARN] Could not remove intermediate build folder: "%TW_BUILD_DIR%"
        )
      )
    ) else (
      echo [WARN] Unexpected: final file missing after copy. Check permissions/paths.
    )
  )
) else (
  echo [WARN] Build finished but index.html not found where expected:
  echo       %TW_OUT%
)

:post_cleanup_success
del /q "%ROOT%*.zevtc" >nul 2>&1
del /q "%ROOT%*.evtc"  >nul 2>&1

echo.
echo ==========================================
echo Pipeline complete.
echo Drag_and_Drop JSON available under:
echo   %DROP_DIR%
echo Raid Summary HTML is at:

set "SUMMARY_HTML="
if defined TW_FINAL (
  set "SUMMARY_HTML=!TW_FINAL!"
) else (
  set "SUMMARY_HTML=%TW_OUT%"
)

echo   !SUMMARY_HTML!

if "%DISCORD_POSTED%"=="1" (
  if defined DISCORD_POSTED_NAME (
    echo Posted Discord notification ^(!DISCORD_POSTED_NAME!^)
  ) else (
    for %%F in ("!SUMMARY_HTML!") do set "TMPFN=%%~nxF"
    echo Posted Discord notification ^(!TMPFN!^)
  )
) else (
  if not defined DISCORD_REASON set "DISCORD_REASON=Skipped"
  echo Discord notification skipped ^(reason: !DISCORD_REASON!^)
)

echo ==========================================

rem --- Update check (silent on failure) ---
if exist "%ROOT%VERSION" (
  set "CURRENT_VERSION="
  for /f "usebackq delims=" %%V in ("%ROOT%VERSION") do if not defined CURRENT_VERSION set "CURRENT_VERSION=%%V"
  if defined CURRENT_VERSION (
    set "UPD_TMPOUT=%TEMP%\gw2_update_check_%RANDOM%_%RANDOM%.out"
    powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPTS_DIR%\Check-Latest-Release.ps1" -CurrentVersion "!CURRENT_VERSION!" > "!UPD_TMPOUT!" 2>nul
    set "UPD_LATEST="
    set "UPD_AVAIL="
    for /f "usebackq tokens=1,* delims==" %%A in ("!UPD_TMPOUT!") do (
      if /i "%%A"=="LATEST" set "UPD_LATEST=%%B"
      if /i "%%A"=="UPDATE_AVAILABLE" set "UPD_AVAIL=%%B"
    )
    del /q "!UPD_TMPOUT!" >nul 2>&1
    if "!UPD_AVAIL!"=="1" (
      echo.
      echo [UPDATE] New release !UPD_LATEST! available ^(current: !CURRENT_VERSION!^). Run update.bat to upgrade.
    )
  )
)
exit /b 0

:fail
echo [ERROR] Pipeline aborted. See messages above.
exit /b 1

rem ==========================================
rem Subroutines
rem ==========================================

:check_python_deps
rem Sets PY_DEPS_OK=1 iff requests, xlsxwriter, glicko2 all import under the
rem same interpreter (%PYTHON_EXE%) that runs the EI Combiner; 0 otherwise.
set "PY_DEPS_OK=0"
"%PYTHON_EXE%" -c "import requests, xlsxwriter, glicko2" >nul 2>&1
if not errorlevel 1 set "PY_DEPS_OK=1"
goto :eof

:print_usage
echo Usage: process_logs.bat [--manual ^| --scheduled]
echo.
echo   (no argument)  Auto-copy logs from LOG_SOURCE_DIR when it is set in
echo                  config.txt; otherwise process whatever is already in
echo                  Raid_Logs. (Legacy behavior - unchanged.)
echo   --manual, -m   Skip the auto-copy step and process the logs currently
echo                  in Raid_Logs as-is.
echo   --scheduled,   Force the raid-window auto-copy from LOG_SOURCE_DIR
echo     -s           (requires LOG_SOURCE_DIR to be set), then process.
echo   --setup        Run first-time setup only (build Elite Insights, create
echo                  configs, install dependencies) and exit. Used by the GUI.
echo   --help, -h     Show this help.
goto :eof

:load_config
rem Parse NAME=VALUE pairs from CONFIG_FILE. Lines starting with # are ignored.
if not exist "%CONFIG_FILE%" goto :eof
for /f "usebackq tokens=* delims=" %%L in ("%CONFIG_FILE%") do (
  set "CFG_LINE=%%L"
  call :parse_config_line
)
set "CFG_LINE="
goto :eof

:parse_config_line
if not defined CFG_LINE goto :eof
if "%CFG_LINE:~0,1%"=="#" goto :eof
if /i "%CFG_LINE:~0,10%"=="GUILD_TAG="            set "GUILD_TAG=%CFG_LINE:~10%"
if /i "%CFG_LINE:~0,20%"=="DISCORD_WEBHOOK_URL="  set "DISCORD_WEBHOOK_URL=%CFG_LINE:~20%"
if /i "%CFG_LINE:~0,16%"=="MAX_PARALLEL_EI="      set "MAX_PARALLEL_EI=%CFG_LINE:~16%"
if /i "%CFG_LINE:~0,15%"=="LOG_SOURCE_DIR="       set "LOG_SOURCE_DIR=%CFG_LINE:~15%"
if /i "%CFG_LINE:~0,16%"=="MIN_LOG_SIZE_KB="      set "MIN_LOG_SIZE_KB=%CFG_LINE:~16%"
if /i "%CFG_LINE:~0,22%"=="RAID_LOGS_GRACE_HOURS=" set "RAID_LOGS_GRACE_HOURS=%CFG_LINE:~22%"
rem RAID_HOURS_* are not loaded into this batch; Resolve-RaidWindow.ps1 reads them directly.
goto :eof

:notify_discord
rem Args:
rem   %~1 = HTML path
rem   %~2 = Discord webhook URL (may be empty)
setlocal EnableExtensions EnableDelayedExpansion

set "HTML_PATH=%~1"
set "WEBHOOK_URL=%~2"
set "RET_POSTED=0"
set "RET_REASON="
set "RET_NAME="

if not exist "!HTML_PATH!" (
  echo [WARN] Discord notify: HTML missing: !HTML_PATH!
  set "RET_REASON=MissingFile"
  goto :notify_done
)

if not defined WEBHOOK_URL (
  echo [INFO] No webhook URL set; skipping Discord notification.
  set "RET_REASON=No webhook URL set"
  goto :notify_done
)

set "TMPOUT=%TEMP%\discord_post_%RANDOM%_%RANDOM%.out"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPTS_DIR%\Post-DiscordSummary.ps1" -WebhookUrl "!WEBHOOK_URL!" -FilePath "!HTML_PATH!" -ZipFallbackDir "%DROP_DIR%" > "!TMPOUT!"
set "PS_RC=!ERRORLEVEL!"

for /f "usebackq tokens=1,* delims==" %%A in ("!TMPOUT!") do (
  if /i "%%A"=="RESULT" set "DS_RESULT=%%B"
  if /i "%%A"=="NAME"   set "DS_NAME=%%B"
)
del /q "!TMPOUT!" >nul 2>&1

if "!PS_RC!"=="0" (
  set "RET_POSTED=1"
  set "RET_REASON=!DS_RESULT!"
  set "RET_NAME=!DS_NAME!"
) else (
  set "RET_POSTED=0"
  if defined DS_RESULT (
    set "RES=!DS_RESULT!"
    if "!RES:~0,5!"=="SKIP:" set "RES=!RES:~5!"
    set "RET_REASON=!RES!"
  ) else (
    set "RET_REASON=Error"
  )
)

:notify_done
endlocal & set "DISCORD_POSTED=%RET_POSTED%" & set "DISCORD_REASON=%RET_REASON%" & set "DISCORD_POSTED_NAME=%RET_NAME%"
goto :eof
