$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$tunnelPidFile = Join-Path $root 'tunnel.pid'
if (Test-Path -LiteralPath $tunnelPidFile) { $savedPid = [int](Get-Content -LiteralPath $tunnelPidFile -Raw); Stop-Process -Id $savedPid -Force -ErrorAction SilentlyContinue; Remove-Item -LiteralPath $tunnelPidFile -Force }
$listener = Get-NetTCPConnection -LocalPort 4783 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) { Stop-Process -Id $listener.OwningProcess -Force -ErrorAction SilentlyContinue }
Remove-Item -LiteralPath (Join-Path $root 'backend.pid') -Force -ErrorAction SilentlyContinue
Write-Host 'MultiMP3 backend and public tunnel stopped.'
