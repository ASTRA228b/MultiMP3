$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$controlLog = Join-Path $root 'Backend Control.log'
function Write-Log($message) { $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] START: $message"; Add-Content -LiteralPath $controlLog -Value $line; Write-Host $line }
trap { Write-Log "ERROR: $($_.Exception.Message)"; exit 1 }
function Test-Backend { return $null -ne (Get-NetTCPConnection -LocalPort 4783 -State Listen -ErrorAction SilentlyContinue | Select-Object -First 1) }
function Install-Worker($name, $file) {
    Write-Log "Registering $name with windowless launcher $([IO.Path]::GetFileName($file))."
    $action = New-ScheduledTaskAction -Execute 'wscript.exe' -Argument "`"$file`"" -WorkingDirectory $root
    $trigger = New-ScheduledTaskTrigger -Once -At ((Get-Date).AddYears(1))
    $settings = New-ScheduledTaskSettingsSet -Hidden -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -ExecutionTimeLimit ([TimeSpan]::Zero)
    Register-ScheduledTask -TaskName $name -Action $action -Trigger $trigger -Settings $settings -Description 'MultiMP3 machine-managed background service' -Force | Out-Null
    Start-ScheduledTask -TaskName $name
}
Write-Log 'Start requested.'
if (-not (Test-Backend)) {
    Install-Worker 'MultiMP3 Backend' (Join-Path $root 'Run-Backend-Hidden.vbs')
    foreach ($attempt in 1..60) { Start-Sleep -Seconds 1; if (Test-Backend) { break } }
    if (-not (Test-Backend)) { throw 'The backend did not open port 4783 within 60 seconds.' }
} else { Write-Log 'Backend was already listening on port 4783.' }
$health = $null
$lastHealthError = 'No healthy response received.'
$healthDeadline = (Get-Date).AddSeconds(120)
do {
    try {
        $candidate = Invoke-RestMethod -Uri 'http://127.0.0.1:4783/api/health' -TimeoutSec 15
        if ($candidate.ok -eq $true -and $candidate.service -eq 'MultiMP3') { $health = $candidate; break }
        $lastHealthError = 'Response did not identify a healthy MultiMP3 backend.'
    } catch {
        $lastHealthError = $_.Exception.Message
        if ($_.ErrorDetails.Message) { $lastHealthError += ' ' + $_.ErrorDetails.Message }
    }
    Write-Log "Local health pending: $lastHealthError"
    Start-Sleep -Seconds 2
} while ((Get-Date) -lt $healthDeadline)
if (-not $health) { throw "Local readiness failed after 120 seconds: $lastHealthError. See backend.err.log." }

Write-Log "Local backend health passed (version $($health.version))."
& (Join-Path $root 'Stop-Tunnel.ps1')
Start-Sleep -Seconds 2
$tunnelLog = Join-Path $root 'tunnel.log'
if (Test-Path -LiteralPath $tunnelLog) { Remove-Item -LiteralPath $tunnelLog -Force -ErrorAction Stop }
Remove-Item -LiteralPath (Join-Path $root 'Tunnel URL.txt') -Force -ErrorAction SilentlyContinue
Install-Worker 'MultiMP3 Tunnel' (Join-Path $root 'Run-Tunnel-Hidden.vbs')
$tunnelUrl = $null
foreach ($attempt in 1..60) {
    Start-Sleep -Seconds 1
    if (Test-Path -LiteralPath $tunnelLog) { $match = [regex]::Match((Get-Content -LiteralPath $tunnelLog -Raw), 'https://[a-z0-9-]+\.trycloudflare\.com'); if ($match.Success) { $tunnelUrl = $match.Value; break } }
}
if (-not $tunnelUrl) { throw 'TryCloudflare did not provide a public URL within 60 seconds.' }
Write-Log "Tunnel assigned $tunnelUrl; waiting for public health."
$publicReady = $false
$lastHealthError = 'No healthy MultiMP3 response.'
foreach ($attempt in 1..45) { try { $remote = Invoke-RestMethod -Uri "$tunnelUrl/api/health" -TimeoutSec 10; if ($remote.ok -eq $true -and $remote.service -eq 'MultiMP3') { $publicReady = $true; break } } catch { $lastHealthError = $_.Exception.Message }; Start-Sleep -Seconds 2 }
if (-not $publicReady) { throw "Public health check failed for $tunnelUrl. Last error: $lastHealthError. Tunnel log: $tunnelLog" }
Set-Content -LiteralPath (Join-Path $root 'Tunnel URL.txt') -Value $tunnelUrl -Encoding ascii
Write-Log "SUCCESS: backend and public tunnel are healthy at $tunnelUrl."
