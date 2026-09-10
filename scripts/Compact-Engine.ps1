param([string]$Destination = "$PSScriptRoot\..\release\MultiMP3\tools")
$ErrorActionPreference = 'Stop'
$root = (Resolve-Path -LiteralPath $Destination).Path
$licenses = Join-Path $root 'licenses'
New-Item -ItemType Directory -Path $licenses -Force | Out-Null
$directories = Get-ChildItem -LiteralPath $root -Directory | Where-Object { $_.Name -match '^ffmpeg-[\d.]+-essentials_build$|^node-v[\d.]+-win-x64$' }
foreach ($directory in $directories) {
    $target = [System.IO.Path]::GetFullPath($directory.FullName)
    if (-not $target.StartsWith($root + [System.IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) { throw 'Cleanup target outside tools directory.' }
    $license = Join-Path $target 'LICENSE'
    if (-not (Test-Path -LiteralPath $license)) { throw "Missing upstream license in $target" }
    Copy-Item -LiteralPath $license -Destination (Join-Path $licenses ($directory.Name + '-LICENSE.txt')) -Force
    Get-ChildItem -LiteralPath $target -File -Filter 'README*' | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination (Join-Path $licenses ($directory.Name + '-' + $_.Name)) -Force }
    Remove-Item -LiteralPath $target -Recurse -Force
}
Get-ChildItem -LiteralPath $root -File -Filter '*.zip' | ForEach-Object { Remove-Item -LiteralPath $_.FullName -Force }
Write-Host 'Engine archives compacted; upstream licenses preserved in tools/licenses.'
