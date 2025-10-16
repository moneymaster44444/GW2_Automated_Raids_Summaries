@echo off
setlocal EnableExtensions EnableDelayedExpansion

call :compute_date_tag
echo DATE_TAG=!DATE_TAG!
exit /b 0

:compute_date_tag
rem --- Helper: compute MM-dd-yy date tag into DATE_TAG ---
set "DATE_TAG="

rem 1) Try Windows PowerShell (prefer 64-bit System32; fall back to Sysnative and PATH)
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

rem 2) Fallback: WMIC (may be missing on newer Windows)
if not defined DATE_TAG (
  for /f "tokens=2 delims==" %%L in ('wmic os get LocalDateTime /value 2^>nul ^| find "="') do set "LDT=%%L"
  if defined LDT (
    rem LDT = YYYYMMDDhhmmss.mmm+TZ -> MM-dd-yy
    set "DATE_TAG=!LDT:~4,2!-!LDT:~6,2!-!LDT:~2,2!"
  )
)

rem 3) Last resort: parse %DATE% (locale-dependent heuristic)
if not defined DATE_TAG (
  for /f "tokens=1-4 delims=/-. " %%a in ("%DATE%") do (
    set "a=%%a" & set "b=%%b" & set "c=%%c" & set "d=%%d"
  )
  if defined b if defined c if defined d (
    rem Common: Wed 10/15/2025  OR  10/15/2025  -> a=Wed/10, b=10/15, c=15/2025, d=2025
    set "DATE_TAG=!b!-!c!-!d:~-2!"
  ) else if defined a if defined b if defined c (
    rem Fallback guess: 10 15 2025
    set "DATE_TAG=!a!-!b!-!c:~-2!"
  )
)

if not defined DATE_TAG set "DATE_TAG=unknown"
exit /b 0
