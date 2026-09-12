@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Stop-Backend.ps1"
set "RESULT=%errorlevel%"
if not "%RESULT%"=="0" (echo Stop failed. See Backend Control.log for details.& pause)
exit /b %RESULT%
