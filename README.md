# FramePress

Native C# / WPF batch video and optional image compressor for Windows 11 x64.

## v0.1.1

Search and compress nested folders in one step, resolution-specific CRF/codec/speed settings, actual before/after sizes, CPU/GPU benchmarking, Arabic RTL and dark/light themes.

Replacement removes the original only after a smaller verified output is ready; no permanent media backup remains. It keeps the base filename and changes the extension to MP4/WebP. Existing sibling files are protected. Copy mode retains originals and uses descriptive output names.

The sample button opens one video directly and estimates its full size/time, plus a one-hour example. It measures SSIM on up to 20 seconds and deletes the temporary sample. SSIM is not a percentage of perceived quality lost. Full replacement verification adds time beyond the sample encode estimate.

## Build and test

Install .NET 10 SDK, put ffmpeg.exe and ffprobe.exe directly in vendor, then:

```powershell
dotnet run --project tests/FramePress.Tests/FramePress.Tests.csproj -c Release
dotnet build src/FramePress.App/FramePress.App.csproj -c Release
dotnet ./src/FramePress.App/bin/Release/net10.0-windows/FramePress.App.dll --smoke-test
dotnet run --project src/FramePress.App/FramePress.App.csproj -c Release
```

On Windows with NSIS 3 installed, `./Build-Release.ps1` creates a self-contained installer. GitHub Actions performs tests, builds the installer, runs WPF startup/render checks and uploads artifacts. Releases are published manually after installation/media checks.

See [update instructions and test checklist](docs/UPDATE-0.1.1-AR.md), [release notes](RELEASE-NOTES.md), [verification](docs/VERIFICATION.md), and [third-party notices](THIRD-PARTY.md).

Source: MIT. FFmpeg is third-party software; follow the distribution/source obligations described in THIRD-PARTY.md.
