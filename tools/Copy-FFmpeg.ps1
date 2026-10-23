param([string]$BinDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$vendor = Join-Path $root 'vendor'
if (!$BinDirectory) {
    $command = Get-Command ffmpeg.exe -ErrorAction SilentlyContinue
    if ($command) { $BinDirectory = Split-Path $command.Source -Parent }
}
if (!$BinDirectory) { throw 'Pass the bin folder containing the real ffmpeg.exe and ffprobe.exe: ./tools/Copy-FFmpeg.ps1 -BinDirectory C:\ffmpeg\bin' }
foreach ($name in @('ffmpeg.exe','ffprobe.exe')) {
    $source = Join-Path $BinDirectory $name
    if (!(Test-Path $source)) { throw "Missing $source" }
}
New-Item -ItemType Directory -Path $vendor -Force | Out-Null
foreach ($name in @('ffmpeg.exe','ffprobe.exe')) { Copy-Item (Join-Path $BinDirectory $name) (Join-Path $vendor $name) -Force }
& (Join-Path $vendor 'ffmpeg.exe') -version
if ($LASTEXITCODE -ne 0) { throw 'The copied FFmpeg executable failed. Do not copy package-manager shims.' }
Write-Host 'FFmpeg copied. It will be bundled in the app at the next build.'
