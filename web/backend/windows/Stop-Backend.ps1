$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$controlLog = Join-Path $root 'Backend Control.log'
function Write-Log($message) { $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] STOP: $message"; Add-Content -LiteralPath $controlLog -Value $line; Write-Host $line }
trap { Write-Log "ERROR: $($_.Exception.Message)"; exit 1 }
Write-Log 'Stop requested.'
& (Join-Path $root 'Stop-Tunnel.ps1')
foreach ($name in @('MultiMP3 Tunnel','MultiMP3 Backend')) { Write-Log "Stopping $name."; Stop-ScheduledTask -TaskName $name -ErrorAction SilentlyContinue }
foreach ($attempt in 1..20) { $listener = Get-NetTCPConnection -LocalPort 4783 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1; if (-not $listener) { break }; Start-Sleep -Milliseconds 500 }
$listener = Get-NetTCPConnection -LocalPort 4783 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1
if ($listener) { Write-Log "Forcing backend process $($listener.OwningProcess) to stop."; Stop-Process -Id $listener.OwningProcess -Force; Start-Sleep -Seconds 1 }
if (Get-NetTCPConnection -LocalPort 4783 -State Listen -ErrorAction SilentlyContinue) { throw 'Port 4783 is still listening after stop.' }
Remove-Item -LiteralPath (Join-Path $root 'backend.pid') -Force -ErrorAction SilentlyContinue
Remove-Item -LiteralPath (Join-Path $root 'Tunnel URL.txt') -Force -ErrorAction SilentlyContinue
Write-Log 'SUCCESS: backend and tunnel are stopped.'
