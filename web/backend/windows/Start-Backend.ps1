$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$healthUrl = 'http://127.0.0.1:4783/api/health'
function Test-Health { try { return (Invoke-RestMethod -Uri $healthUrl -TimeoutSec 2).ok -eq $true } catch { return $false } }
if (-not (Test-Health)) {
    Remove-Item -LiteralPath (Join-Path $root 'backend.pid') -Force -ErrorAction SilentlyContinue
    $env:YT_DLP_PATH = Join-Path $root 'tools\yt-dlp.exe'
    $env:FFMPEG_PATH = Join-Path $root 'tools\ffmpeg.exe'
    $env:MULTIMP3_HOST = '127.0.0.1'; $env:MULTIMP3_PORT = '4783'
    $origins = @('http://127.0.0.1:4173', 'http://localhost:4173')
    $originFile = Join-Path $root 'Public Site URL.txt'
    if (Test-Path -LiteralPath $originFile) { $origin = (Get-Content -LiteralPath $originFile -Raw).Trim(); if ($origin -match '^https://[^/]+$') { $origins += $origin } }
    $env:MULTIMP3_ALLOWED_ORIGINS = $origins -join ','
    Start-Process -FilePath (Join-Path $root 'node.exe') -ArgumentList (Join-Path $root 'server.js') -WorkingDirectory $root -WindowStyle Hidden | Out-Null
    foreach ($attempt in 1..20) { Start-Sleep -Milliseconds 500; if (Test-Health) { break } }
    if (-not (Test-Health)) { throw 'The MultiMP3 backend did not start.' }
}
$tunnelPidFile = Join-Path $root 'tunnel.pid'; $tunnelRunning = $false
if (Test-Path -LiteralPath $tunnelPidFile) { $savedPid = [int](Get-Content -LiteralPath $tunnelPidFile -Raw); $tunnelRunning = $null -ne (Get-Process -Id $savedPid -ErrorAction SilentlyContinue) }
if (-not $tunnelRunning) {
    $logFile = Join-Path $root 'tunnel.log'; Remove-Item -LiteralPath $logFile -Force -ErrorAction SilentlyContinue
    $process = Start-Process -FilePath (Join-Path $root 'cloudflared.exe') -ArgumentList @('tunnel','--url','http://127.0.0.1:4783','--no-autoupdate','--logfile',$logFile) -WorkingDirectory $root -WindowStyle Hidden -PassThru
    Set-Content -LiteralPath $tunnelPidFile -Value $process.Id -Encoding ascii
}
$tunnelUrl = $null; $logFile = Join-Path $root 'tunnel.log'
foreach ($attempt in 1..40) { Start-Sleep -Milliseconds 500; if (Test-Path -LiteralPath $logFile) { $match = [regex]::Match((Get-Content -LiteralPath $logFile -Raw), 'https://[a-z0-9-]+\.trycloudflare\.com'); if ($match.Success) { $tunnelUrl = $match.Value; break } } }
if (-not $tunnelUrl) { throw 'The backend started, but TryCloudflare did not provide a public URL.' }
Set-Content -LiteralPath (Join-Path $root 'Tunnel URL.txt') -Value $tunnelUrl -Encoding ascii
Write-Host "MultiMP3 backend: $healthUrl"; Write-Host "Public API: $tunnelUrl"
