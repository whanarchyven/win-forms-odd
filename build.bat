@echo off
rem Build KioskStand.exe with the C# compiler that ships with Windows (.NET Framework 4.x)
rem and copy it into every stand folder (KioskStand-Catalysis, KioskStand-Dermedic).
setlocal
set FW=%WINDIR%\Microsoft.NET\Framework64\v4.0.30319
if not exist "%FW%\csc.exe" set FW=%WINDIR%\Microsoft.NET\Framework\v4.0.30319
set WPF=%FW%\WPF
set OUT=%~dp0src\bin
if not exist "%OUT%" mkdir "%OUT%"
"%FW%\csc.exe" -nologo -target:winexe -optimize+ -codepage:65001 -out:"%OUT%\KioskStand.exe" -r:"%WPF%\PresentationFramework.dll" -r:"%WPF%\PresentationCore.dll" -r:"%WPF%\WindowsBase.dll" -r:System.Xaml.dll -r:System.Core.dll -r:System.IO.Compression.dll -r:System.IO.Compression.FileSystem.dll "%~dp0src\*.cs"
if errorlevel 1 (
  echo.
  echo BUILD FAILED
  if "%1"=="" pause
  exit /b 1
)
for %%D in (Catalysis Dermedic) do (
  copy /y "%OUT%\KioskStand.exe" "%~dp0KioskStand-%%D\KioskStand.exe" >nul
  if errorlevel 1 (echo COPY FAILED: KioskStand-%%D) else (echo OK: KioskStand-%%D\KioskStand.exe)
)
if "%1"=="" pause
