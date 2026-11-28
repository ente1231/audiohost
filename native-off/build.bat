@echo off
rem build.bat - baut AudioAgent.dll (x64) und injector.exe
rem Braucht VS Build Tools mit C++ ("x64 Native Tools Command Prompt") oder
rem vcvars64.bat im PATH. Erzeugt: dist\AudioAgent.dll, dist\injector.exe

setlocal
cd /d "%~dp0"

where cl.exe >nul 2>&1
if errorlevel 1 (
  for %%P in (
    "%ProgramFiles%\Microsoft Visual Studio\2022\BuildTools\VC\Auxiliary\Build\vcvars64.bat"
    "%ProgramFiles%\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvars64.bat"
    "%ProgramFiles%\Microsoft Visual Studio\2022\Professional\VC\Auxiliary\Build\vcvars64.bat"
  ) do if exist %%P call %%P >nul
)

where cl.exe >nul 2>&1
if errorlevel 1 (
  echo cl.exe nicht gefunden - Visual Studio C++ Build Tools installieren
  exit /b 1
)

if not exist dist mkdir dist

echo [1/2] AudioAgent.dll
cl /nologo /LD /std:c++17 /W4 /permissive- /O2 /DUNICODE /D_UNICODE ^
   /Fe:dist\AudioAgent.dll /Fo:dist\ ^
   AudioAgent.cpp user32.lib advapi32.lib shell32.lib winhttp.lib
if errorlevel 1 exit /b 1

echo [2/2] injector.exe
cl /nologo /std:c++17 /W4 /permissive- /O2 /DUNICODE /D_UNICODE ^
   /Fe:dist\injector.exe /Fo:dist\ ^
   injector.cpp
if errorlevel 1 exit /b 1

echo.
echo fertig:
dir /b dist\AudioAgent.dll dist\injector.exe
exit /b 0
