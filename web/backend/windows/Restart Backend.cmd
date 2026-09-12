@echo off
setlocal
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Restart-Backend.ps1"
exit /b %errorlevel%
