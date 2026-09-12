$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$healthUrl = 'http://127.0.0.1:4783/api/health'
function Test-Backend { return $null -ne (Get-NetTCPConnection -LocalPort 4783 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1) }
function Install-Worker($name, $file) {
    $action = New-ScheduledTaskAction -Execute 'cmd.exe' -Argument "/c `"`"$file`"`"" -WorkingDirectory $root
    $trigger = New-ScheduledTaskTrigger -Once -At ((Get-Date).AddYears(1))
    $principal = New-ScheduledTaskPrincipal -UserId ([System.Security.Principal.WindowsIdentity]::GetCurrent().Name) -LogonType S4U -RunLevel Limited
    $settings = New-ScheduledTaskSettingsSet -Hidden -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
    Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger -Principal $principal -Settings $settings -Description 'MultiMP3 machine-managed background service' -Force | Out-Null
    Start-ScheduledTask -TaskName $name
}
if (-not (Test-Backend)) {
    Install-Worker 'MultiMP3 Backend' (Join-Path $root 'Run-Backend.cmd')
    foreach ($attempt in 1..40) { Start-Sleep -Milliseconds 500; if (Test-Backend) { break } }
    if (-not (Test-Backend)) { throw 'The MultiMP3 backend did not start.' }
}
$logFile = Join-Path $root 'tunnel.log'; Remove-Item -LiteralPath $logFile -Force -ErrorAction SilentlyContinue
Install-Worker 'MultiMP3 Tunnel' (Join-Path $root 'Run-Tunnel.cmd')
$tunnelUrl = $null
foreach ($attempt in 1..40) { Start-Sleep -Milliseconds 500; if (Test-Path -LiteralPath $logFile) { $match = [regex]::Match((Get-Content -LiteralPath $logFile -Raw), 'https://[a-z0-9-]+\.trycloudflare\.com'); if ($match.Success) { $tunnelUrl = $match.Value; break } } }
if (-not $tunnelUrl) { throw 'The backend started, but TryCloudflare did not provide a public URL.' }
Set-Content -LiteralPath (Join-Path $root 'Tunnel URL.txt') -Value $tunnelUrl -Encoding ascii
Write-Host "MultiMP3 backend: $healthUrl"; Write-Host "Public API: $tunnelUrl"
