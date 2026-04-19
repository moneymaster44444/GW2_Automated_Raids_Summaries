@echo off
setlocal EnableExtensions
set "ROOT=%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%ROOT%Scripts\Update-FromRelease.ps1" -InstallRoot "%ROOT%" %*
exit /b %ERRORLEVEL%
