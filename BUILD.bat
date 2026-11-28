@echo off
setlocal
cd /d "%~dp0"
if exist "update\READY" del "update\READY"
dotnet publish PanicControl.csproj -c Release --no-self-contained -o update
if errorlevel 1 (
  echo Build fehlgeschlagen.
  pause
  exit /b 1
)
echo ready>"update\READY"
echo Fertig. Laufende PC-App schliessen; START.bat uebernimmt das Update und oeffnet das Programm.
pause
