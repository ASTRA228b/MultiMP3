$ErrorActionPreference = 'Stop'
$env:DOTNET_CLI_HOME = Join-Path $PSScriptRoot '..\.build'
dotnet publish "$PSScriptRoot\..\src\MultiMP3\MultiMP3.csproj" -c Release -r win-x64 --self-contained true -o "$PSScriptRoot\..\release\MultiMP3"
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }
Copy-Item -LiteralPath "$PSScriptRoot\..\README.md" -Destination "$PSScriptRoot\..\release\MultiMP3\README.md" -Force
Write-Host 'MultiMP3 is ready in release\MultiMP3. Run Setup-Engine.ps1 to install the media tools.'
