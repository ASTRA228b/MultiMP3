param([string]$OutputDirectory = "$PSScriptRoot\..\artifacts")
$ErrorActionPreference = 'Stop'
$projectRoot = (Resolve-Path -LiteralPath "$PSScriptRoot\..").Path
$env:DOTNET_CLI_HOME = Join-Path $projectRoot '.build'
[xml]$project = Get-Content -LiteralPath "$projectRoot\src\MultiMP3\MultiMP3.csproj"
$version = @($project.Project.PropertyGroup.Version | Where-Object { $_ })[0]
if ($version -notmatch '^\d+\.\d+\.\d+$') { throw 'Project version must be a semantic version.' }
$stage = Join-Path $OutputDirectory ('staging-' + [Guid]::NewGuid().ToString('N'))
$app = Join-Path $stage 'MultiMP3'
New-Item -ItemType Directory -Path $app -Force | Out-Null
dotnet publish "$projectRoot\src\MultiMP3\MultiMP3.csproj" -c Release -r win-x64 --self-contained true -p:EnableUiTests=false -p:DebugType=None -p:DebugSymbols=false -p:ContinuousIntegrationBuild=true "-p:PathMap=$projectRoot=/src" -o $app
if ($LASTEXITCODE -ne 0) { throw 'Release publish failed.' }
foreach ($file in @('README.md', 'LICENSE', 'CHANGELOG.md', 'THIRD-PARTY-NOTICES.md')) { Copy-Item -LiteralPath (Join-Path $projectRoot $file) -Destination $app }
foreach ($directory in @('assets', 'docs')) {
    $destination = Join-Path $app $directory
    New-Item -ItemType Directory -Path $destination -Force | Out-Null
    Get-ChildItem -LiteralPath (Join-Path $projectRoot $directory) | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $destination -Recurse -Force }
}
$scriptDirectory = Join-Path $app 'scripts'; New-Item -ItemType Directory -Path $scriptDirectory | Out-Null
foreach ($file in @('Setup-Engine.ps1', 'Setup-Node.ps1', 'Compact-Engine.ps1', 'Create-Shortcut.ps1')) { Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $scriptDirectory }
Copy-Item -LiteralPath "$projectRoot\scripts\Setup-Engine.cmd" -Destination $app
Copy-Item -LiteralPath "$projectRoot\scripts\Create-Shortcut.cmd" -Destination $app
$licenses = Join-Path $app 'licenses'; New-Item -ItemType Directory -Path $licenses | Out-Null
$runtime = Get-Content -LiteralPath (Join-Path $app 'MultiMP3.runtimeconfig.json') -Raw | ConvertFrom-Json
$assets = Get-Content -LiteralPath "$projectRoot\src\MultiMP3\obj\project.assets.json" -Raw | ConvertFrom-Json
foreach ($framework in $runtime.runtimeOptions.includedFrameworks) {
    $package = $framework.name.ToLowerInvariant() + '.runtime.win-x64'
    $relative = "$package/$($framework.version)"
    $pack = $assets.packageFolders.PSObject.Properties.Name | ForEach-Object { Join-Path $_ $relative } | Where-Object { Test-Path -LiteralPath $_ } | Select-Object -First 1
    if (-not $pack) { throw "Runtime license pack not found: $relative" }
    $noticeFiles = @('LICENSE', 'LICENSE.TXT', 'THIRD-PARTY-NOTICES.TXT') | Where-Object { Test-Path -LiteralPath (Join-Path $pack $_) }
    if (-not ($noticeFiles -contains 'LICENSE' -or $noticeFiles -contains 'LICENSE.TXT')) { throw "Runtime license missing: $relative" }
    foreach ($file in $noticeFiles) {
        $source = Join-Path $pack $file
        if (-not (Test-Path -LiteralPath $source)) { throw "Runtime notice missing: $file" }
        Copy-Item -LiteralPath $source -Destination (Join-Path $licenses ($framework.name + '-' + $framework.version + '-' + $file))
    }
}
$forbidden = Get-ChildItem -LiteralPath $app -Recurse -File | Where-Object { $_.Extension -in @('.pdb', '.mp3', '.mp4', '.wav', '.part', '.tmp') -or $_.Name -like 'library.json*' }
if ($forbidden) { throw 'Unexpected private, media, or debug output in release staging.' }
$zip = Join-Path $OutputDirectory "MultiMP3-v$version-Windows-x64.zip"
Compress-Archive -LiteralPath $app -DestinationPath $zip -Force
$hash = (Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash.ToLowerInvariant()
[System.IO.File]::WriteAllText([System.IO.Path]::GetFullPath("$zip.sha256"), "$hash  $([System.IO.Path]::GetFileName($zip))`n")
Write-Output "Release package: $([System.IO.Path]::GetFullPath($zip))"
Write-Output "Staged application: $([System.IO.Path]::GetFullPath($app))"
