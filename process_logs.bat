@echo off
setlocal EnableExtensions EnableDelayedExpansion

rem ==========================================
rem Process arcDPS logs -> EI JSON -> Drag_and_Drop JSON -> TW5 Auto-Imported HTML
rem ==========================================

rem Resolve repo root (this script must live at repo root)
set "ROOT=%~dp0"

rem Canonical paths (aligned with your config scripts)
set "LOGS_DIR=%ROOT%Raid_Logs"
set "EI_JSON_DIR=%ROOT%Raids_Summaries\EI_json_output"
set "DROP_DIR=%ROOT%Raids_Summaries"

rem Config files created by establish_config_files.bat
set "EI_CONF=%ROOT%Resources\Config\EliteInsights.conf"
set "COMBINER_INI=%ROOT%Resources\Config\top_stats_config.ini"

rem Elite Insights CLI publish/output dir (target of dotnet publish)
set "EI_CLI_DIR=%ROOT%Resources\Elite Insights\GW2EI.bin\Release\CLI"

rem Possible EI CLI exe names
set "EI_EXE_NAME1=GW2EIParserCLI.exe"
set "EI_EXE_NAME2=GuildWars2EliteInsights-CLI.exe"

rem EI CLI project (for fallback publish)
set "EI_CSPROJ=%ROOT%Resources\Elite Insights\GW2EIParserCLI\GW2EIParserCLI.csproj"

rem EI Combiner (Python script)
set "PYTHON_EXE=python"
set "COMBINER_PY=%ROOT%Resources\EI Combiner\tw5_top_stats.py"

rem Discord webhook secrets file (first non-empty, non-# line is used)
set "DISCORD_WEBHOOKS_FILE=%ROOT%Resources\Config\Secrets\discord_webhook.txt"

rem Discord status flags (for final summary)
set "DISCORD_POSTED=0"
set "DISCORD_REASON="
set "DISCORD_POSTED_NAME="

rem ==========================================
rem First-time setup: ensure configs and EI CLI exist
rem ==========================================

set "NEED_CONFIG_SETUP=0"
if not exist "%EI_CONF%" set "NEED_CONFIG_SETUP=1"
if not exist "%COMBINER_INI%" set "NEED_CONFIG_SETUP=1"
if not exist "%DISCORD_WEBHOOKS_FILE%" set "NEED_CONFIG_SETUP=1"

if "%NEED_CONFIG_SETUP%"=="1" (
  if exist "%ROOT%establish_config_files.bat" (
    echo [SETUP] Running establish_config_files.bat to create config files...
    call "%ROOT%establish_config_files.bat"
    if errorlevel 1 (
      echo [ERROR] establish_config_files.bat failed.
      goto :_fail
    )
  ) else (
    echo [ERROR] establish_config_files.bat not found at:
    echo         %ROOT%establish_config_files.bat
    goto :_fail
  )
)

if not exist "%EI_CONF%" (
  echo [ERROR] EliteInsights.conf not found after setup: %EI_CONF%
  goto :_fail
)
if not exist "%COMBINER_INI%" (
  echo [ERROR] top_stats_config.ini not found after setup: %COMBINER_INI%
  goto :_fail
)
if not exist "%DISCORD_WEBHOOKS_FILE%" (
  echo [WARN] Discord webhook file not found after setup:
  echo        %DISCORD_WEBHOOKS_FILE%
)

set "NEED_EI_BUILD=0"
if not exist "%EI_CLI_DIR%\GuildWars2EliteInsights-CLI.exe" (
  if not exist "%EI_CLI_DIR%\GW2EIParserCLI.exe" (
    set "NEED_EI_BUILD=1"
  )
)

if "%NEED_EI_BUILD%"=="1" (
  if exist "%ROOT%build_elite_insights.bat" (
    echo [SETUP] Running build_elite_insights.bat to build EI CLI...
    call "%ROOT%build_elite_insights.bat"
    if errorlevel 1 (
      echo [ERROR] build_elite_insights.bat failed.
      goto :_fail
    )
  ) else (
    echo [ERROR] build_elite_insights.bat not found at:
    echo         %ROOT%build_elite_insights.bat
    goto :_fail
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

if not exist "%EI_CONF%" (
  echo [ERROR] EliteInsights.conf not found: %EI_CONF%
  goto :_fail
)
if not exist "%COMBINER_INI%" (
  echo [ERROR] top_stats_config.ini not found: %COMBINER_INI%
  goto :_fail
)

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

rem ==========================================================
rem [4] Auto-import into TiddlyWiki and build single-file HTML
rem ==========================================================

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
      goto :_fail
    )
  )
)

set "TW_SHELL=%ROOT%Resources\EI Combiner\Example_Output\Top_Stats_Index.html"
set "AUTO_TID=%ROOT%auto-import.tid"

where tiddlywiki >nul 2>&1
if errorlevel 1 (
  echo [ERROR] "tiddlywiki" not found on PATH. Did you run: npm install -g tiddlywiki ?
  goto :_fail
)

if not exist "%AUTO_TID%" (
  echo [ERROR] auto-import.tid not found at: %AUTO_TID%
  goto :_fail
)

for %%A in ("%LATEST_JSON%") do set "LATEST_NAME=%%~nxA"
set "LATEST_DROP_JSON=%DROP_DIR%\%LATEST_NAME%"

if not exist "%LATEST_DROP_JSON%" (
  echo [ERROR] Expected Drag_and_Drop JSON not found in Raids_Summaries:
  echo         %LATEST_DROP_JSON%
  goto :_fail
)

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

  call :compute_date_tag
  echo [INFO] Using date tag: !DATE_TAG!

  set "TW_FINAL=%DROP_DIR%\INC_!DATE_TAG!.html"

  copy /y "%TW_OUT%" "!TW_FINAL!" >nul
  if errorlevel 1 (
    echo [WARN] Copy failed; leaving original at:
    echo       %TW_OUT%
  ) else (
    del /q "%TW_OUT%" >nul 2>&1
    if exist "!TW_FINAL!" (
      echo [OK] Final HTML written to:
      echo      !TW_FINAL!

      if exist "%DISCORD_WEBHOOKS_FILE%" (
        call :notify_discord "!TW_FINAL!" "%DISCORD_WEBHOOKS_FILE%"
      ) else (
        echo [INFO] Discord webhook file not found at %DISCORD_WEBHOOKS_FILE%; skipping notification.
        set "DISCORD_POSTED=0"
        set "DISCORD_REASON=No webhook URL set"
        for %%F in ("!TW_FINAL!") do set "DISCORD_POSTED_NAME=%%~nxF"
      )

      rem Optional: remove TiddlyWiki build folder after final HTML is safely copied
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

:_post_cleanup_success
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
exit /b 0

:_fail
echo [ERROR] Pipeline aborted. See messages above.
exit /b 1

:_run_ei
if not exist "%~1" goto :eof
set "FOUND_LOG=1"
echo     -^> "%~nx1"
"%EI_CLI_EXE%" -c "%EI_CONF%" "%~1"
if errorlevel 1 echo     [WARN] EI returned non-zero for %~nx1
goto :eof

:compute_date_tag
set "DATE_TAG="

for %%P in (
  "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
  "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
  powershell.exe
) do (
  if not defined DATE_TAG (
    for /f "delims=" %%D in ('"%%~P" -NoProfile -Command "(Get-Date).ToString('MM-dd-yy')" 2^>nul') do (
      if not defined DATE_TAG set "DATE_TAG=%%D"
    )
  )
)

if not defined DATE_TAG (
  for /f "tokens=2 delims==" %%L in ('wmic os get LocalDateTime /value 2^>nul ^| find "="') do set "LDT=%%L"
  if defined LDT set "DATE_TAG=!LDT:~4,2!-!LDT:~6,2!-!LDT:~2,2!"
)

if not defined DATE_TAG (
  for /f "tokens=1-4 delims=/-. " %%a in ("%DATE%") do (
    set "a=%%a"
    set "b=%%b"
    set "c=%%c"
    set "d=%%d"
  )
  if defined b if defined c if defined d (
    set "DATE_TAG=!b!-!c!-!d:~-2!"
  ) else if defined a if defined b if defined c (
    set "DATE_TAG=!a!-!b!-!c:~-2!"
  )
)

if not defined DATE_TAG set "DATE_TAG=unknown"
exit /b 0

:notify_discord
rem Args:
rem   %~1 = HTML path
rem   %~2 = webhooks file path

setlocal EnableExtensions EnableDelayedExpansion

set "HTML_PATH=%~1"
set "HOOKS_FILE=%~2"
set "RET_POSTED=0"
set "RET_REASON="
set "RET_NAME="

set "PSBIN="
set "WEBHOOK_URL="
set "TMPPS=%TEMP%\post_discord_%RANDOM%_%RANDOM%.ps1"
set "TMPZIP="

if not exist "!HTML_PATH!" (
  echo [WARN] Discord notify: HTML missing: !HTML_PATH!
  set "RET_REASON=MissingFile"
  goto :notify_discord_cleanup
)

for %%P in (
  "%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe"
  "%SystemRoot%\Sysnative\WindowsPowerShell\v1.0\powershell.exe"
  powershell.exe
) do (
  if not defined PSBIN if exist "%%~fP" set "PSBIN=%%~fP"
)

if not defined PSBIN (
  echo [WARN] PowerShell not found; skipping Discord notification.
  set "RET_REASON=NoPowerShell"
  goto :notify_discord_cleanup
)

if exist "!HOOKS_FILE!" (
  for /f "usebackq tokens=* delims=" %%L in ("!HOOKS_FILE!") do (
    if not defined WEBHOOK_URL (
      set "LINE=%%L"
      if defined LINE (
		  if not "!LINE:~0,1!"=="#" (
			set "WEBHOOK_URL=!LINE!"
		  )
		)
    )
  )
)

if not defined WEBHOOK_URL (
  echo [WARN] No webhook URL found in: !HOOKS_FILE!
  set "RET_REASON=No webhook URL set"
  goto :notify_discord_cleanup
)

>  "!TMPPS!" echo param([string]$Url, [string]$FilePath)
>> "!TMPPS!" echo try {
>> "!TMPPS!" echo   if^(-not ^(Test-Path -LiteralPath $FilePath^)^){ throw "File not found: $FilePath" }
>> "!TMPPS!" echo   $name = [System.IO.Path]::GetFileNameWithoutExtension^($FilePath^)
>> "!TMPPS!" echo   $mm = $dd = $yy = $null
>> "!TMPPS!" echo   if^($name -match '^INC_^(\d{2}^)-^(\d{2}^)-^(\d{2}^)$'^){ $mm=$Matches[1]; $dd=$Matches[2]; $yy=$Matches[3] }
>> "!TMPPS!" echo   if^($mm -and $dd -and $yy^) {
>> "!TMPPS!" echo     $year = 2000 + [int]$yy
>> "!TMPPS!" echo     $d = Get-Date -Year $year -Month ^([int]$mm^) -Day ^([int]$dd^)
>> "!TMPPS!" echo     $weekday = $d.ToString^('dddd'^)
>> "!TMPPS!" echo     $msg = ("Summary for {0} {1}/{2} raid" -f $weekday, $mm, $dd)
>> "!TMPPS!" echo   } else {
>> "!TMPPS!" echo     $d = Get-Date
>> "!TMPPS!" echo     $msg = ("Summary for {0} {1} raid" -f $d.ToString('dddd'), $d.ToString('MM/dd'))
>> "!TMPPS!" echo   }
>> "!TMPPS!" echo   Add-Type -AssemblyName System.Net.Http
>> "!TMPPS!" echo   $client  = [System.Net.Http.HttpClient]::new^(^)
>> "!TMPPS!" echo   $content = [System.Net.Http.MultipartFormDataContent]::new^(^)
>> "!TMPPS!" echo   $payload = @{ content = $msg } ^| ConvertTo-Json -Compress
>> "!TMPPS!" echo   $stringContent = [System.Net.Http.StringContent]::new^($payload,[System.Text.Encoding]::UTF8,"application/json"^)
>> "!TMPPS!" echo   $null = $content.Add^($stringContent, "payload_json"^)
>> "!TMPPS!" echo   $fs = [System.IO.File]::OpenRead^($FilePath^)
>> "!TMPPS!" echo   try {
>> "!TMPPS!" echo     $fileContent = [System.Net.Http.StreamContent]::new^($fs^)
>> "!TMPPS!" echo     if^($FilePath.ToLower^(^).EndsWith^('.html'^)^){ $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse^("text/html"^) }
>> "!TMPPS!" echo     else { $fileContent.Headers.ContentType = [System.Net.Http.Headers.MediaTypeHeaderValue]::Parse^("application/zip"^) }
>> "!TMPPS!" echo     $null = $content.Add^($fileContent, "files[0]", [System.IO.Path]::GetFileName^($FilePath^)^)
>> "!TMPPS!" echo     $response = $client.PostAsync^($Url, $content^).Result
>> "!TMPPS!" echo   } finally {
>> "!TMPPS!" echo     $fs.Dispose^(^)
>> "!TMPPS!" echo     $client.Dispose^(^)
>> "!TMPPS!" echo   }
>> "!TMPPS!" echo   if^(-not $response.IsSuccessStatusCode^) {
>> "!TMPPS!" echo     $code = [int]$response.StatusCode
>> "!TMPPS!" echo     $reason = [string]$response.ReasonPhrase
>> "!TMPPS!" echo     throw ("HTTP {0} {1}" -f $code, $reason)
>> "!TMPPS!" echo   }
>> "!TMPPS!" echo } catch { Write-Host ("[PS ERR] " + $_.Exception.Message); exit 1 }

echo [INFO] Discord: uploading HTML attachment...
"%PSBIN%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "!TMPPS!" "!WEBHOOK_URL!" "!HTML_PATH!"
set "RC=%ERRORLEVEL%"

if not "!RC!"=="0" goto :notify_zip_fallback

echo [OK] Posted Discord notification (HTML).
set "RET_POSTED=1"
set "RET_REASON=HTML"
for %%F in ("!HTML_PATH!") do set "RET_NAME=%%~nxF"
goto :notify_discord_cleanup

:notify_zip_fallback
echo [INFO] HTML upload failed - trying with ZIP file instead...

for %%F in ("!HTML_PATH!") do set "TMPZIP=%DROP_DIR%\%%~nF.zip"

if exist "!TMPZIP!" del /q "!TMPZIP!" >nul 2>&1

set "ZIP_PS=Compress-Archive -LiteralPath $env:HTML_PATH -DestinationPath $env:TMPZIP -Force"
"%PSBIN%" -NoLogo -NoProfile -ExecutionPolicy Bypass -Command "!ZIP_PS!"

echo [INFO] Building ZIP file

if not exist "!TMPZIP!" (
  echo [WARN] ZIP fallback failed - skipping Discord notification.
  set "RET_REASON=ZipCreateFailed"
  goto :notify_discord_cleanup
)

echo [OK] ZIP file written to:
echo      !TMPZIP!

echo [INFO] Discord: uploading ZIP fallback...
"%PSBIN%" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "!TMPPS!" "!WEBHOOK_URL!" "!TMPZIP!"
set "RC2=%ERRORLEVEL%"

if not "!RC2!"=="0" goto :notify_zip_failed

echo [OK] Posted Discord notification (ZIP).
set "RET_POSTED=1"
set "RET_REASON=ZIP"
for %%F in ("!TMPZIP!") do set "RET_NAME=%%~nxF"
goto :notify_discord_cleanup

:notify_zip_failed
echo [WARN] Discord upload (ZIP) failed with code !RC2!.
set "RET_REASON=Error"
goto :notify_discord_cleanup

:notify_discord_cleanup
if defined TMPPS if exist "!TMPPS!" del /q "!TMPPS!" >nul 2>&1
rem if defined TMPZIP if exist "!TMPZIP!" del /q "!TMPZIP!" >nul 2>&1

endlocal & set "DISCORD_POSTED=%RET_POSTED%" & set "DISCORD_REASON=%RET_REASON%" & set "DISCORD_POSTED_NAME=%RET_NAME%"
goto :eof