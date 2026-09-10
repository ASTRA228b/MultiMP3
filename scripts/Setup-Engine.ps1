param([string]$Destination = "$PSScriptRoot\..\release\MultiMP3\tools")
$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null
$download = Join-Path $Destination 'yt-dlp.download'
Invoke-WebRequest 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/yt-dlp.exe' -OutFile $download
Invoke-WebRequest 'https://github.com/yt-dlp/yt-dlp/releases/latest/download/SHA2-256SUMS' -OutFile (Join-Path $Destination 'SHA2-256SUMS')
$expected = (Get-Content (Join-Path $Destination 'SHA2-256SUMS') | Where-Object { $_ -match '\s+yt-dlp.exe$' }).Split(' ')[0]
if ((Get-FileHash $download -Algorithm SHA256).Hash.ToLowerInvariant() -ne $expected.ToLowerInvariant()) { throw 'yt-dlp checksum mismatch.' }
Move-Item -LiteralPath $download -Destination (Join-Path $Destination 'yt-dlp.exe') -Force
$archive = Join-Path $Destination 'ffmpeg.zip'
Invoke-WebRequest 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip' -OutFile $archive
Invoke-WebRequest 'https://www.gyan.dev/ffmpeg/builds/ffmpeg-release-essentials.zip.sha256' -OutFile "$archive.sha256"
$ffmpegExpected = ((Get-Content "$archive.sha256" -Raw).Trim() -split '\s+')[0]
if ((Get-FileHash $archive -Algorithm SHA256).Hash.ToLowerInvariant() -ne $ffmpegExpected.ToLowerInvariant()) { throw 'FFmpeg checksum mismatch.' }
Expand-Archive -LiteralPath $archive -DestinationPath $Destination -Force
$binary = Get-ChildItem -LiteralPath $Destination -Directory -Filter 'ffmpeg-*-essentials_build' | ForEach-Object { Get-Item -LiteralPath (Join-Path $_.FullName 'bin/ffmpeg.exe') } | Select-Object -First 1
if (-not $binary) { throw 'The FFmpeg archive did not contain the expected executable.' }
Copy-Item -LiteralPath $binary.FullName -Destination (Join-Path $Destination 'ffmpeg.exe') -Force
Copy-Item -LiteralPath (Join-Path $binary.DirectoryName 'ffprobe.exe') -Destination (Join-Path $Destination 'ffprobe.exe') -Force
Write-Host "Engine installed in $Destination. Upstream license files remain in the extracted FFmpeg directory."
& "$PSScriptRoot\Setup-Node.ps1" -Destination $Destination
& "$PSScriptRoot\Compact-Engine.ps1" -Destination $Destination
