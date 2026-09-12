@echo off
setlocal
cd /d "%~dp0"
"%CD%\cloudflared.exe" tunnel --url http://127.0.0.1:4783 --no-autoupdate --logfile "%CD%\tunnel.log"
