param([string]$Executable = "$PSScriptRoot\..\release\MultiMP3\MultiMP3.exe")
$ErrorActionPreference = 'Stop'
$exe = (Resolve-Path -LiteralPath $Executable).Path
$desktop = [Environment]::GetFolderPath('DesktopDirectory')
$shortcutPath = Join-Path $desktop 'MultiMP3.lnk'
$shell = New-Object -ComObject WScript.Shell
if (Test-Path -LiteralPath $shortcutPath) {
    $existing = $shell.CreateShortcut($shortcutPath)
    if ($existing.TargetPath -ne $exe) { $shortcutPath = Join-Path $desktop 'MultiMP3 (Desktop App).lnk' }
}
$shortcut = $shell.CreateShortcut($shortcutPath)
$shortcut.TargetPath = $exe
$shortcut.WorkingDirectory = Split-Path -Parent $exe
$shortcut.IconLocation = "$exe,0"
$shortcut.Description = 'MultiMP3 - Your sound. Organized.'
$shortcut.Save()
$verified = $shell.CreateShortcut($shortcutPath)
if ($verified.TargetPath -ne $exe -or -not (Test-Path -LiteralPath $shortcutPath)) { throw 'Shortcut verification failed.' }
Write-Host "Desktop shortcut created: $shortcutPath"
