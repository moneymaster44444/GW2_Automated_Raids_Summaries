@echo off
setlocal EnableExtensions

rem ==========================================
rem Main entry point: sets up and launches GW2 Raid Summaries.
rem Builds the GUI app (incremental) then starts it, passing the repo root so
rem the app can find process_logs.bat, Raid_Logs, and config.txt. On first
rem launch the GUI runs the one-time setup (Elite Insights, configs, deps).
rem ==========================================

set "ROOT=%~dp0"
set "ROOT_NOBS=%ROOT:~0,-1%"
set "PROJ=%ROOT%Gui\GW2RaidsGui.csproj"
set "EXE=%ROOT%Gui\bin\Release\net8.0-windows\GW2RaidsGui.exe"

where dotnet >nul 2>&1
if errorlevel 1 (
  echo [ERROR] .NET SDK not found on PATH.
  echo         Install the .NET 8 SDK from https://dotnet.microsoft.com/download
  pause
  exit /b 1
)

echo [SETUP] Building GW2 Raid Summaries GUI ^(first run may take a moment^)...
dotnet build "%PROJ%" -c Release --nologo -v minimal
if errorlevel 1 (
  echo [ERROR] Build failed. See messages above.
  pause
  exit /b 1
)

if not exist "%EXE%" (
  echo [ERROR] Build succeeded but the executable was not found:
  echo         %EXE%
  pause
  exit /b 1
)

start "" "%EXE%" "%ROOT_NOBS%"
endlocal
exit /b 0
