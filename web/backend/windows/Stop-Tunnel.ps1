$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $MyInvocation.MyCommand.Path
Stop-ScheduledTask -TaskName 'MultiMP3 Tunnel' -ErrorAction SilentlyContinue
# The scheduled VBS/cmd wrapper can exit without terminating cloudflared.
# Only stop the executable installed in this backend directory.
$expectedPath = [IO.Path]::GetFullPath((Join-Path $root 'cloudflared.exe'))
foreach ($worker in @(Get-CimInstance Win32_Process -Filter "Name = 'cloudflared.exe'")) {
    if ($worker.ExecutablePath -and $worker.ExecutablePath.Equals($expectedPath, [StringComparison]::OrdinalIgnoreCase)) {
        Write-Host "Stopping MultiMP3 tunnel PID $($worker.ProcessId)."
        Stop-Process -Id $worker.ProcessId -Force -ErrorAction Stop
        Wait-Process -Id $worker.ProcessId -Timeout 10 -ErrorAction SilentlyContinue
    }
}
$remaining = @(Get-CimInstance Win32_Process -Filter "Name = 'cloudflared.exe'" | Where-Object {
    $_.ExecutablePath -and $_.ExecutablePath.Equals($expectedPath, [StringComparison]::OrdinalIgnoreCase)
})
if ($remaining.Count) { throw 'MultiMP3 tunnel processes survived cleanup.' }
