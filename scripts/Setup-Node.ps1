param([string]$Destination = "$PSScriptRoot\..\release\MultiMP3\tools")
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$sums = (Invoke-WebRequest 'https://nodejs.org/dist/latest-v22.x/SHASUMS256.txt' -UseBasicParsing).Content
$line = ($sums -split "`n" | Where-Object { $_ -match 'node-v[\d.]+-win-x64.zip$' } | Select-Object -First 1).Trim()
if (-not $line) { throw 'No Windows x64 Node release found.' }
$fields = $line -split '\s+'
$name = $fields[1]
$archive = Join-Path $Destination $name
Invoke-WebRequest "https://nodejs.org/dist/latest-v22.x/$name" -OutFile $archive
if ((Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $fields[0]) { throw 'Node checksum mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $Destination -Force
$extracted = Join-Path $Destination ([System.IO.Path]::GetFileNameWithoutExtension($name))
Copy-Item -LiteralPath (Join-Path $extracted 'node.exe') -Destination (Join-Path $Destination 'node.exe') -Force
Write-Host 'Portable Node runtime installed. License retained in the extracted directory.'
