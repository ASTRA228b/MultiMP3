@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Start-Backend.ps1"
set "RESULT=%errorlevel%"
if not "%RESULT%"=="0" (echo Start failed. See Backend Control.log for details.& pause)
exit /b %RESULT%
