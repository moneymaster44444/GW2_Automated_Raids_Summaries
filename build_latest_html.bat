@echo off
setlocal EnableExtensions EnableDelayedExpansion

set "ROOT=%~dp0"
set "TW_BUILD_DIR=%ROOT%gw2build"
set "TW_SHELL=%ROOT%Resources\EI Combiner\Example_Output\Top_Stats_Index.html"
set "AUTO_TID=%ROOT%auto-import.tid"
set "DROP_DIR=%ROOT%Raids_Summaries"

echo [A] where tiddlywiki
where tiddlywiki || (echo [FATAL] tiddlywiki not found & exit /b 1)

echo [B] resolve JSON
set "LATEST_DROP_JSON="
for /f "delims=" %%J in ('dir /b /a:-d /o:-d "%DROP_DIR%\Drag_and_Drop_Log_Summary_*.json" 2^>nul') do (
  if not defined LATEST_DROP_JSON set "LATEST_DROP_JSON=%DROP_DIR%\%%~J"
)
if not defined LATEST_DROP_JSON (
  echo [FATAL] No Drag_and_Drop_Log_Summary_*.json in "%DROP_DIR%"
  exit /b 2
)
echo     -> "%LATEST_DROP_JSON%"

echo [C] check auto-import.tid
if not exist "%AUTO_TID%" (echo [FATAL] missing "%AUTO_TID%" & exit /b 3)

echo [D] ensure wiki exists (init+load once)
if not exist "%TW_BUILD_DIR%" (
  echo     init...
  call tiddlywiki "%TW_BUILD_DIR%" --init server || (echo [FATAL] init failed & exit /b 10)
  if not exist "%TW_SHELL%" (echo [FATAL] missing shell "%TW_SHELL%" & exit /b 11)
  echo     load shell...
  call tiddlywiki "%TW_BUILD_DIR%" --load "%TW_SHELL%" || (echo [FATAL] load failed & exit /b 12)
)

echo [E] import/refresh startup tiddler
call tiddlywiki "%TW_BUILD_DIR%" --import "%AUTO_TID%" text/plain || (echo [FATAL] auto-import.tid failed & exit /b 13)

echo [F] import JSON -> $:/data/dragdrop
call tiddlywiki "%TW_BUILD_DIR%" --import "%LATEST_DROP_JSON%" application/json "$:/data/dragdrop" || (echo [FATAL] JSON import failed & exit /b 14)

echo [G] build index
call tiddlywiki "%TW_BUILD_DIR%" --build index || (echo [FATAL] build failed & exit /b 15)

set "OUT=%TW_BUILD_DIR%\output\index.html"
if exist "%OUT%" (
  echo [OK] built -> "%OUT%"
) else (
  echo [WARN] build finished but not found: "%OUT%"
)
