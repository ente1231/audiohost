@echo off
setlocal EnableExtensions
title Panic Control - Status
echo ============================================
echo   Panic Control - was ist installiert?
echo ============================================
echo.

set "U=%LOCALAPPDATA%\Microsoft\Windows\Caches\ClassroomData"
set "M=%ProgramData%\Microsoft\Windows\ClassroomHelper"

echo [1] Benutzer-Installation
echo     %U%
if exist "%U%\classroomhelper.exe" (
    echo     OK  - classroomhelper.exe vorhanden
) else if exist "%U%" (
    echo     Ordner da, aber die exe FEHLT  ^(Virenscanner?^)
) else (
    echo     nicht vorhanden
)
echo.

echo [2] Maschinen-Installation ^(alle Benutzer^)
echo     %M%
if exist "%M%\ClassroomHelper.exe" (
    echo     OK  - vorhanden, gilt fuer alle Benutzer
) else if exist "%M%" (
    echo     Ordner da, aber die exe FEHLT  ^(Virenscanner?^)
) else (
    echo     nicht vorhanden - laeuft nur im aktuellen Benutzerkonto
)
echo.

echo [3] Autostart-Eintraege
reg query "HKCU\Software\Microsoft\Windows\CurrentVersion\Run" /v ClassroomHelper >nul 2>&1
if errorlevel 1 (echo     HKCU Run : fehlt) else (echo     HKCU Run : OK)
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v ClassroomHelperService >nul 2>&1
if errorlevel 1 (echo     HKLM Run : fehlt) else (echo     HKLM Run : OK)
reg query "HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Run" /v ClassroomHelper >nul 2>&1
if errorlevel 1 (echo     HKLM Sec : fehlt) else (echo     HKLM Sec : OK)
echo.

echo [4] Geplante Aufgaben
schtasks /Query /TN "Microsoft\Windows\Multimedia\SystemAudioCalibration" >nul 2>&1
if errorlevel 1 (echo     Maschinen-Task : fehlt) else (echo     Maschinen-Task : OK ^(alle Benutzer, ONLOGON^))
schtasks /Query /TN "ClassroomHelperSvc" >nul 2>&1
if errorlevel 1 (echo     Benutzer-Task  : fehlt) else (echo     Benutzer-Task  : OK ^(dieses Konto, ONLOGON^))
echo.

echo [5] Laufende Prozesse
tasklist /FI "IMAGENAME eq classroomhelper.exe" 2>nul | find /i "classroomhelper.exe" >nul
if errorlevel 1 (echo     classroomhelper.exe : nicht aktiv) else (echo     classroomhelper.exe : laeuft)
tasklist /FI "IMAGENAME eq ClassroomHelper.exe" 2>nul | find /i "ClassroomHelper.exe" >nul
if errorlevel 1 (echo     ClassroomHelper.exe : nicht aktiv) else (echo     ClassroomHelper.exe : laeuft)
tasklist /FI "IMAGENAME eq cloudflared.exe" 2>nul | find /i "cloudflared.exe" >nul
if errorlevel 1 (echo     cloudflared.exe : nicht aktiv) else (echo     cloudflared.exe : laeuft)
echo.

echo [6] Tunnel-Adresse
if exist "%U%\tunnel.url" (
    type "%U%\tunnel.url"
    echo.
) else if exist "%M%\tunnel.url" (
    type "%M%\tunnel.url"
    echo.
) else (
    echo     keine tunnel.url - Tunnel noch nicht gestartet
)
echo.

echo [7] Server hoert auf 8443?
netstat -ano | findstr ":8443" | findstr "LISTENING"
if errorlevel 1 echo     nein
echo.
echo ============================================
pause
