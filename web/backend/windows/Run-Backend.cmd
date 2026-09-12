@echo off
setlocal
cd /d "%~dp0"
set "YT_DLP_PATH=%CD%\tools\yt-dlp.exe"
set "FFMPEG_PATH=%CD%\tools\ffmpeg.exe"
set "MULTIMP3_HOST=127.0.0.1"
set "MULTIMP3_PORT=4783"
set "MULTIMP3_ALLOWED_ORIGINS=http://127.0.0.1:4173,http://localhost:4173"
if exist "Public Site URL.txt" (
  set /p MULTIMP3_PUBLIC_ORIGIN=<"Public Site URL.txt"
  call set "MULTIMP3_ALLOWED_ORIGINS=%%MULTIMP3_ALLOWED_ORIGINS%%,%%MULTIMP3_PUBLIC_ORIGIN%%"
)
"%CD%\node.exe" "%CD%\server.js"
