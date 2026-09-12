$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
foreach ($name in @('MultiMP3 Tunnel','MultiMP3 Backend')) { Stop-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue }
$tunnels = Get-Process -Name cloudflared -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq (Join-Path $root 'cloudflared.exe') }
if ($tunnels) { $tunnels | Stop-Process -Force -ErrorAction SilentlyContinue }
$listener = Get-NetTCPConnection -LocalPort 4783 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) { Stop-Process -Id $listener.OwningProcess -Force -ErrorAction SilentlyContinue }
Remove-Item -LiteralPath (Join-Path $root 'backend.pid') -Force -ErrorAction SilentlyContinue
Write-Host 'MultiMP3 backend and public tunnel stopped.'
