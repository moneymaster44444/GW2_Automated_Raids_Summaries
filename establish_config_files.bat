@echo off
setlocal

REM --- Repo root (trailing backslash) ---
set "ROOT=%~dp0"

REM --- Canonical EI JSON output folder ---
set "EI_JSON_DIR=%ROOT%Raids_Summaries\EI_json_output"
if not exist "%EI_JSON_DIR%" mkdir "%EI_JSON_DIR%"

REM --- Ensure the config folder exists (destination for real files) ---
if not exist "%ROOT%Resources\Config" mkdir "%ROOT%Resources\Config"

REM --- Paths to samples/real configs ---
set "SAMPLE_EI=%ROOT%Resources\Config\sample.eliteinsights.conf"
set "REAL_EI=%ROOT%Resources\Config\EliteInsights.conf"
set "SAMPLE_COMB=%ROOT%Resources\Config\sample.top_stats_config.ini"
set "REAL_COMB=%ROOT%Resources\Config\top_stats_config.ini"

if not exist "%SAMPLE_EI%" (
  echo [ERROR] Missing "%SAMPLE_EI%"
  exit /b 1
)
if not exist "%SAMPLE_COMB%" (
  echo [ERROR] Missing "%SAMPLE_COMB%"
  exit /b 1
)

echo Writing EliteInsights.conf with OutLocation=%EI_JSON_DIR%
echo Writing top_stats_config.ini with input_directory=%EI_JSON_DIR%

REM --- Build a temp PowerShell script (line-by-line to avoid CMD paren issues) ---
set "TMPPS=%TEMP%\establish_config_files.ps1"
> "%TMPPS%" echo param([string]$SampleEi,[string]$RealEi,[string]$EiJsonDir,[string]$SampleComb,[string]$RealComb)
>> "%TMPPS%" echo $utf8NoBom = New-Object System.Text.UTF8Encoding($false)
>> "%TMPPS%" echo # EliteInsights.conf: replace __OUTLOCATION__
>> "%TMPPS%" echo $c = Get-Content -Raw -LiteralPath $SampleEi
>> "%TMPPS%" echo $c = $c.Replace('__OUTLOCATION__', $EiJsonDir)
>> "%TMPPS%" echo [System.IO.File]::WriteAllText($RealEi, $c, $utf8NoBom)
>> "%TMPPS%" echo # top_stats_config.ini: replace __INPUT_JSON_DIR__
>> "%TMPPS%" echo $c = Get-Content -Raw -LiteralPath $SampleComb
>> "%TMPPS%" echo $c = $c.Replace('__INPUT_JSON_DIR__', $EiJsonDir)
>> "%TMPPS%" echo [System.IO.File]::WriteAllText($RealComb, $c, $utf8NoBom)

REM --- Run the PowerShell script with arguments ---
powershell -NoProfile -ExecutionPolicy Bypass -File "%TMPPS%" ^
  -SampleEi "%SAMPLE_EI%" -RealEi "%REAL_EI%" -EiJsonDir "%EI_JSON_DIR%" ^
  -SampleComb "%SAMPLE_COMB%" -RealComb "%REAL_COMB%"

set "EC=%ERRORLEVEL%"
del /q "%TMPPS%" >nul 2>&1

if not "%EC%"=="0" (
  echo [ERROR] Failed to generate config files. PowerShell exit code %EC%.
  exit /b %EC%
)

echo.
echo Generated:
echo   %REAL_EI%
echo   %REAL_COMB%
exit /b 0
