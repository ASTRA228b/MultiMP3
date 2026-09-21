param([switch]$PublishOnly)
$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$controlLog = Join-Path $root 'Backend Control.log'
function Write-Log($message) { $line = "[$(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')] RESTART: $message"; Add-Content -LiteralPath $controlLog -Value $line; Write-Host $line }
trap { Write-Log "ERROR: $($_.Exception.Message)"; exit 1 }
function Invoke-Git([string[]]$Arguments) {
    $previousPreference = $ErrorActionPreference
    $ErrorActionPreference = 'Continue'
    $output = & git @Arguments 2>&1
    $code = $LASTEXITCODE
    $ErrorActionPreference = $previousPreference
    foreach ($line in $output) { Write-Log "git: $line" }
    if ($code -ne 0) { throw "git $($Arguments -join ' ') failed with exit code $code." }
}
Write-Log 'Restart requested.'
if (-not $PublishOnly) {
& (Join-Path $root 'Stop-Backend.ps1'); if (-not $?) { throw 'Stop step failed.' }
& (Join-Path $root 'Start-Backend.ps1'); if (-not $?) { throw 'Start step failed.' }
}
$tunnelUrl = (Get-Content -LiteralPath (Join-Path $root 'Tunnel URL.txt') -Raw).Trim()
$repoCandidate = Join-Path $root '..\..\..'
if (Test-Path -LiteralPath (Join-Path $repoCandidate '.git')) { $repo = (Resolve-Path $repoCandidate).Path }
else { $repoFile = Join-Path $root 'Repository Path.txt'; if (-not (Test-Path -LiteralPath $repoFile)) { throw 'Repository Path.txt is missing.' }; $repo = (Get-Content -LiteralPath $repoFile -Raw).Trim() }
if ($tunnelUrl -notmatch '^https://[a-z0-9-]+\.trycloudflare\.com$') { throw 'Invalid tunnel URL.' }
$health = Invoke-RestMethod -Uri "$tunnelUrl/api/health" -TimeoutSec 15
if ($health.ok -ne $true -or $health.service -ne 'MultiMP3') { throw 'The active tunnel is not a healthy MultiMP3 backend.' }
$remote = (& git -C $repo remote get-url origin).Trim()
if ($LASTEXITCODE -ne 0 -or $remote -notmatch 'github\.com[:/]ASTRA228b/MultiMP3(?:\.git)?$') { throw "Unexpected Git remote: $remote" }
# Publish only the generated proxy change from the latest remote main. The user's
# checkout, index, local commits, and other files are never merged or committed.
$publishedGit = $false
foreach ($publishAttempt in 1..3) {
    $publishDir = Join-Path ([IO.Path]::GetTempPath()) ('multimp3-publish-' + [guid]::NewGuid().ToString('N'))
    Invoke-Git @('clone','--depth','1','--branch','main','--single-branch',$remote,$publishDir)
    Write-Log "Publishing from current GitHub main in $publishDir (attempt $publishAttempt/3)."
    Push-Location $publishDir
    try {
        $vercelFile = Join-Path $publishDir 'web\vercel.json'
        $config = Get-Content -LiteralPath $vercelFile -Raw | ConvertFrom-Json
        $routes = @($config.rewrites | Where-Object { $_.source -eq '/api/:path*' })
        if ($routes.Count -ne 1) { throw 'Expected exactly one /api/:path* rewrite in web/vercel.json.' }
        $destination = "$tunnelUrl/api/:path*"
        if ($routes[0].destination -eq $destination) {
            Write-Log 'GitHub main already contains the active tunnel URL.'
            $publishedGit = $true
            break
        }
        $routes[0].destination = $destination
        [IO.File]::WriteAllText($vercelFile, ($config | ConvertTo-Json -Depth 20 -Compress), (New-Object Text.UTF8Encoding($false)))
        Invoke-Git @('add','--','web/vercel.json')
        Invoke-Git @('-c','user.name=ASTRA228b','-c','user.email=ASTRA228b@users.noreply.github.com','commit','-m','Update public backend tunnel URL','--','web/vercel.json')
        try {
            Invoke-Git @('push','origin','HEAD:refs/heads/main')
            $publishedGit = $true
            Write-Log 'Pushed the active tunnel proxy to ASTRA228b/MultiMP3.'
            break
        } catch {
            if ($publishAttempt -eq 3) { throw }
            Write-Log 'Push failed; retrying from fresh GitHub main in case another update landed.'
        }
    } finally { Pop-Location }
}
if (-not $publishedGit) { throw 'The tunnel URL was not published to GitHub.' }
Write-Log 'Waiting for the Vercel production proxy to become healthy.'
$siteFile = Join-Path $root 'Public Site URL.txt'
$siteUrl = 'https://multimp3.vercel.app'
if (Test-Path -LiteralPath $siteFile) { $siteUrl = (Get-Content -LiteralPath $siteFile -Raw).Trim().TrimEnd('/') }
$deadline = [DateTime]::UtcNow.AddMinutes(5)
$vercelReady = $false
$lastError = 'No response yet.'
$attempt = 0
while ([DateTime]::UtcNow -lt $deadline) {
    $attempt++
    try {
        $remaining = [Math]::Max(1, [Math]::Min(15, [int]($deadline - [DateTime]::UtcNow).TotalSeconds))
        $published = Invoke-RestMethod -Uri "$siteUrl/api/health" -TimeoutSec $remaining
        if ($published.ok -eq $true -and $published.service -eq 'MultiMP3') { $vercelReady = $true; break }
        $lastError = 'Response did not identify a healthy MultiMP3 service.'
    } catch { $lastError = $_.Exception.Message }
    Write-Log "Vercel check $attempt pending: $lastError"
    if ([DateTime]::UtcNow -lt $deadline) { Start-Sleep -Seconds 5 }
}
if (-not $vercelReady) { throw "GitHub contains the active tunnel, but $siteUrl/api/health did not become healthy within five minutes. Last error: $lastError. Check the Vercel deployment log; use -PublishOnly to retry without restarting services." }
Write-Log 'Vercel public health check passed.'
Write-Log 'SUCCESS: verification and GitHub update completed.'