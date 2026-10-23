$ErrorActionPreference = 'Stop'
Set-Location $PSScriptRoot
if (Test-Path './vendor/ffmpeg.exe') { $env:PATH = "$PSScriptRoot\vendor;$env:PATH" }
dotnet run --project ./tests/FramePress.Tests/FramePress.Tests.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Tests failed; the app was not launched.' }
dotnet run --project ./src/FramePress.App/FramePress.App.csproj -c Release
if ($LASTEXITCODE -ne 0) { throw 'Application failed.' }
