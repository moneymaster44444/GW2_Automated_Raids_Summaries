@echo off
setlocal EnableExtensions

rem ==========================================
rem Generate EliteInsights.conf and top_stats_config.ini
rem from the committed sample.* templates
rem ==========================================

set "ROOT=%~dp0"

set "EI_JSON_DIR=%ROOT%Raids_Summaries\EI_json_output"
if not exist "%EI_JSON_DIR%"            mkdir "%EI_JSON_DIR%"
if not exist "%ROOT%Resources\Config"   mkdir "%ROOT%Resources\Config"

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

powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Scripts\Establish-Configs.ps1" ^
  -SampleEi "%SAMPLE_EI%" -RealEi "%REAL_EI%" -EiJsonDir "%EI_JSON_DIR%" ^
  -SampleComb "%SAMPLE_COMB%" -RealComb "%REAL_COMB%"
if errorlevel 1 (
  echo [ERROR] Failed to generate config files.
  exit /b 1
)

echo.
echo Generated:
echo   %REAL_EI%
echo   %REAL_COMB%
exit /b 0
