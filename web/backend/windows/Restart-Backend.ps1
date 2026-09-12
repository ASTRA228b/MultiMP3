$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
& (Join-Path $root 'Stop-Backend.ps1'); & (Join-Path $root 'Start-Backend.ps1')
$tunnelUrl = (Get-Content -LiteralPath (Join-Path $root 'Tunnel URL.txt') -Raw).Trim()
$repoCandidate = Join-Path $root '..\..\..'
if (Test-Path -LiteralPath (Join-Path $repoCandidate '.git')) { $repo = (Resolve-Path $repoCandidate).Path }
else {
    $repoFile = Join-Path $root 'Repository Path.txt'
    if (-not (Test-Path -LiteralPath $repoFile)) { throw 'Repository Path.txt is missing from the backend folder.' }
    $repo = (Get-Content -LiteralPath $repoFile -Raw).Trim()
}
$apiFile = Join-Path $repo 'web\static\js\api.js'
$source = Get-Content -LiteralPath $apiFile -Raw
$updated = [regex]::Replace($source, "base:\(\)=>\s*'https?://[^']+'", "base:()=> '$tunnelUrl'")
if ($updated -eq $source -and $source -notmatch [regex]::Escape($tunnelUrl)) { throw 'Could not update the web API address.' }
Set-Content -LiteralPath $apiFile -Value $updated -Encoding utf8
Push-Location $repo
try {
    $remote = (& git remote get-url origin).Trim()
    if ($remote -notmatch 'github\.com[:/]ASTRA228b/MultiMP3(?:\.git)?$') { throw "Unexpected Git remote: $remote" }
    & git add -- 'web/static/js/api.js'
    & git diff --cached --quiet
    if ($LASTEXITCODE -ne 0) { & git commit -m 'Update public backend tunnel URL'; if ($LASTEXITCODE -ne 0) { throw 'Git commit failed.' }; & git push origin HEAD; if ($LASTEXITCODE -ne 0) { throw 'Git push failed.' }; Write-Host 'The new tunnel address was pushed to ASTRA228b/MultiMP3.' }
    else { Write-Host 'The public tunnel address is unchanged; no Git push was needed.' }
} finally { Pop-Location }
