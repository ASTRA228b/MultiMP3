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
& (Join-Path $root 'Stop-Backend.ps1'); if (-not $?) { throw 'Stop step failed.' }
& (Join-Path $root 'Start-Backend.ps1'); if (-not $?) { throw 'Start step failed.' }
$tunnelUrl = (Get-Content -LiteralPath (Join-Path $root 'Tunnel URL.txt') -Raw).Trim()
$repoCandidate = Join-Path $root '..\..\..'
if (Test-Path -LiteralPath (Join-Path $repoCandidate '.git')) { $repo = (Resolve-Path $repoCandidate).Path }
else { $repoFile = Join-Path $root 'Repository Path.txt'; if (-not (Test-Path -LiteralPath $repoFile)) { throw 'Repository Path.txt is missing.' }; $repo = (Get-Content -LiteralPath $repoFile -Raw).Trim() }
$vercelFile = Join-Path $repo 'web\vercel.json'
$config = Get-Content -LiteralPath $vercelFile -Raw | ConvertFrom-Json
$config.rewrites[0].destination = "$tunnelUrl/api/:path*"
$config | ConvertTo-Json -Depth 10 -Compress | Set-Content -LiteralPath $vercelFile -Encoding utf8
Write-Log "Updated Vercel proxy target to $tunnelUrl."
Push-Location $repo
try {
    $remote = (& git remote get-url origin).Trim(); if ($remote -notmatch 'github\.com[:/]ASTRA228b/MultiMP3(?:\.git)?$') { throw "Unexpected Git remote: $remote" }
    Invoke-Git @('add','--','web/vercel.json')
    & git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) { Invoke-Git @('commit','-m','Update public backend tunnel URL'); Invoke-Git @('push','origin','HEAD'); Write-Log 'Pushed the new tunnel proxy to ASTRA228b/MultiMP3.' }
    else { Write-Log 'Tunnel proxy was already current; no push needed.' }
} finally { Pop-Location }
Write-Log 'SUCCESS: restart, verification, and GitHub update completed.'
