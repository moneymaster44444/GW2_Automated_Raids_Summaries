@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ==========================================
rem Process arcDPS logs -> EI JSON -> Drag_and_Drop JSON -> TW5 Auto-Imported HTML
rem ==========================================

rem --- Resolve repo root (this script must live at repo root) ---
set "ROOT=%~dp0"

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
call :load_config
if not defined GUILD_TAG set "GUILD_TAG=OnLY"
if not defined MAX_PARALLEL_EI set "MAX_PARALLEL_EI=0"

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
  echo [ERROR] "tiddlywiki" not found on PATH. Did you run: npm install -g tiddlywiki ?
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
  for /f "usebackq delims=" %%D in (`powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%SCRIPTS_DIR%\Get-DateTag.ps1"`) do set "DATE_TAG=%%D"
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
if /i "%CFG_LINE:~0,10%"=="GUILD_TAG="           set "GUILD_TAG=%CFG_LINE:~10%"
if /i "%CFG_LINE:~0,20%"=="DISCORD_WEBHOOK_URL=" set "DISCORD_WEBHOOK_URL=%CFG_LINE:~20%"
if /i "%CFG_LINE:~0,16%"=="MAX_PARALLEL_EI=" set "MAX_PARALLEL_EI=%CFG_LINE:~16%"
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
