@echo off
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Setup-Engine.ps1" -Destination "%~dp0tools"
if errorlevel 1 (
  echo Setup failed. See the message above and README.md.
  pause
  exit /b 1
)
echo Setup complete. Open MultiMP3 and select Settings - Recheck Dependencies.
pause
