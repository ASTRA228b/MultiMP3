@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Create-Shortcut.ps1" -Executable "%~dp0MultiMP3.exe"
if errorlevel 1 (
  pause
  exit /b 1
)
echo Desktop shortcut ready.
pause
