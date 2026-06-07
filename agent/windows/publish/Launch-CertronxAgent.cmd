@echo off

setlocal

set "APP_DIR=%~dp0"

set "EXE=%APP_DIR%DeviceCertAgent.exe"

set "RUNTIME_PAGE=https://dotnet.microsoft.com/download/dotnet/8.0"



if not exist "%EXE%" (

  echo DeviceCertAgent.exe was not found in this folder.

  exit /b 1

)



reg query "HKLM\SOFTWARE\dotnet\Setup\InstalledVersions\x64\sharedfx\Microsoft.WindowsDesktop.App" 2>nul | findstr /R "8\.[0-9]" >nul

if %ERRORLEVEL% NEQ 0 (

  echo.

  echo Certronx Agent requires the .NET 8 Desktop Runtime ^(x64^).

  echo.

  echo Download and install it from:

  echo   %RUNTIME_PAGE%

  echo.

  echo Choose ".NET Desktop Runtime 8.x" for Windows x64.

  echo.

  set /p OPEN=Open download page now? [Y/N]:

  if /i "%OPEN%"=="Y" start "" "%RUNTIME_PAGE%"

  exit /b 1

)



echo Certronx Agent registers certronx:// links automatically on first run.

echo If website deep links fail, double-click register-certronx-protocol.reg once ^(update the exe path inside^).

echo.



start "" /wait "%EXE%" %*

exit /b %ERRORLEVEL%

