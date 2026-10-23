$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
foreach ($name in @('ffmpeg.exe','ffprobe.exe')) {
    if (!(Test-Path (Join-Path './vendor' $name))) { throw "vendor/$name is missing. Run tools/Copy-FFmpeg.ps1 first." }
}
$env:PATH = "$PSScriptRoot\vendor;$env:PATH"
dotnet run --project ./tests/FramePress.Tests/FramePress.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed.' }
$publish = Join-Path $PSScriptRoot 'artifacts\publish'
if (Test-Path $publish) { Remove-Item $publish -Recurse -Force }
dotnet publish ./src/FramePress.App/FramePress.App.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o $publish
if ($LASTEXITCODE -ne 0) { throw 'Publish failed.' }
$lines = New-Object 'System.Collections.Generic.List[string]'
Get-ChildItem $publish -File -Recurse | ForEach-Object {
    $relative = $_.FullName.Substring($publish.Length + 1)
    $lines.Add('Delete "$INSTDIR\' + $relative + '"')
}
Get-ChildItem $publish -Directory -Recurse | Sort-Object { $_.FullName.Length } -Descending | ForEach-Object {
    $relative = $_.FullName.Substring($publish.Length + 1)
    $lines.Add('RMDir "$INSTDIR\' + $relative + '"')
}
$lines | Set-Content './installer/UninstallFiles.nsh' -Encoding UTF8
$nsis = (Get-Command makensis.exe -ErrorAction SilentlyContinue).Source
if (!$nsis) { $nsis = "${env:ProgramFiles(x86)}\NSIS\makensis.exe" }
if (!(Test-Path $nsis)) { throw 'Install NSIS 3 to build the installer. The portable app is already in artifacts/publish.' }
& $nsis ./installer/FramePress.nsi
if ($LASTEXITCODE -ne 0) { throw 'NSIS failed.' }
Get-FileHash './artifacts/FramePress-v0.1.0-Setup.exe' -Algorithm SHA256 | Format-List
