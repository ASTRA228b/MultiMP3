@echo off
setlocal
cd /d "%~dp0"
powershell.exe -NoProfile -Command "$p=(Get-NetTCPConnection -LocalPort 4783 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1).OwningProcess; if ($p) { Stop-Process -Id $p -Force }; Remove-Item -LiteralPath 'backend.pid' -Force -ErrorAction SilentlyContinue"
echo MultiMP3 backend stopped.
