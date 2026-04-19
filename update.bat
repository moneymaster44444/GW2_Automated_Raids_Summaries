@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
set "ROOT_NOSLASH=%ROOT%"
if "%ROOT_NOSLASH:~-1%"=="\" set "ROOT_NOSLASH=%ROOT_NOSLASH:~0,-1%"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Scripts\Update-FromRelease.ps1" -InstallRoot "%ROOT_NOSLASH%" %*
exit /b %ERRORLEVEL%
