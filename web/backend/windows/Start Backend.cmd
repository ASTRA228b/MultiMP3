@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -Command "if (Get-NetTCPConnection -LocalPort 4783 -State Listen -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"
if not errorlevel 1 (echo MultiMP3 backend is already running at http://127.0.0.1:4783 & exit /b 0)
set "YT_DLP_PATH=%CD%\tools\yt-dlp.exe"
set "FFMPEG_PATH=%CD%\tools\ffmpeg.exe"
set "MULTIMP3_HOST=127.0.0.1"
set "MULTIMP3_PORT=4783"
powershell.exe -NoProfile -Command "Start-Process -FilePath '%CD%\node.exe' -ArgumentList '%CD%\server.js' -WorkingDirectory '%CD%' -WindowStyle Hidden"
powershell.exe -NoProfile -Command "Start-Sleep -Seconds 2"
powershell.exe -NoProfile -Command "if (Get-NetTCPConnection -LocalPort 4783 -State Listen -ErrorAction SilentlyContinue) { exit 0 } else { exit 1 }"
if not errorlevel 1 (echo MultiMP3 backend started at http://127.0.0.1:4783) else (echo Backend did not start. & exit /b 1)
