@echo off
REM Build DeviceCertAgent (bypasses PowerShell execution policy)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build-agent.ps1" %*
exit /b %ERRORLEVEL%
