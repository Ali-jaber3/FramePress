# FramePress v0.1.0

Native Windows 11 batch video/image compressor built with **C#, WPF and .NET 10**.

## Features

- One-step recursive search and compression; processing starts before full metadata discovery ends.
- Independent SD, 720p, 1080p, 1440p, 4K and 8K+ profiles, supporting portrait and landscape sources.
- H.264 compatibility default, optional H.265, adjustable quality and speed.
- Windows CPU/GPU names and automatic short-sample benchmarks of NVIDIA NVENC, Intel QSV, AMD AMF and CPU encoders.
- Actual before/after file sizes, reduction percentage, optional result columns and completed-output totals.
- Optional JPEG/PNG/WebP → WebP image compression; animation safeguards.
- Arabic RTL plus nine languages, dark #1B1B1B/light themes and animated sun/moon control.
- Copies or replacement with recovery backups, cancellation, activity log, sample estimates and CSV reports.
- NSIS installer, Start/desktop shortcuts, self-contained Windows build and GitHub Actions checks.

## Development

Windows 11 x64, .NET 10 SDK and FFmpeg 6.1+ with FFprobe, libx264/libx265/libwebp.

```powershell
./tools/Copy-FFmpeg.ps1 -BinDirectory 'C:\ffmpeg\bin'
dotnet run --project ./tests/FramePress.Tests/FramePress.Tests.csproj -c Release
dotnet run --project ./src/FramePress.App/FramePress.App.csproj -c Release
```

FFmpeg tools can be on PATH for development. Real Windows binaries in `vendor/` are bundled on build. The source has no third-party NuGet dependencies.

## Automatic hardware choice

CPU/GPU product names are displayed from Windows CIM. Names do not decide the winner: Auto benchmarks a short source sample at its dimensions using supported hardware encoders and a CPU baseline, then selects the fastest successful measurement at the chosen settings. Results cache for the session per codec, bit depth, dimensions, quality and preset. Unavailable or failing hardware falls back to CPU.

Initial benchmarking adds time. A short sample including initialization is only an approximate performance predictor; it does not establish objectively best visual quality. CRF, CQ and QP are different scales. Test a sample inside your editor before processing camera masters. GPU decoding/filtering is not guaranteed; CPU and disk bottlenecks can remain.

## Output sizes and validation

Before is the original size. After is shown only for a complete successful output, never substituted with a short sample's size. Reduction is `(input - output) / input × 100`. The completed-output card excludes samples/errors. Copies and recovery backups continue to occupy space.

Copies use basic validation by default with originals retained. Full output decode is optional for copies and always required for replacement. Odd video dimensions may be padded by one pixel to support 4:2:0. HDR, multiple video streams and subtitles require manual review. Professional data/timecode streams block replacement. 10-bit+ SDR requires H.265 opt-in and becomes 10-bit 4:2:0. Audio becomes AAC. Some camera/image metadata is not preserved. Keep camera originals, particularly Log/RAW/HDR material.

## First public release and installer

[Arabic GitHub/release guide](docs/FIRST-RELEASE-AR.md) · [Testing guide](docs/TESTING_AR.md) · [Code guide](docs/CODE_AR.md)

```powershell
# FFmpeg in vendor/ and NSIS 3 installed:
./Build-Release.ps1
```

Output: `artifacts/FramePress-v0.1.0-Setup.exe`. Portable app: complete `artifacts/publish/` folder. End users do not need the SDK or a separate .NET/FFmpeg installation when using the properly bundled installer.

GitHub Actions builds on main/tag pushes and on workflow dispatch. It runs integration tests, builds the installer, then exercises real WPF rendering/bindings for Arabic/English and dark/light themes. Artifacts are not published as GitHub Releases automatically. Attach the tested Setup EXE to the `v0.1.0` Release manually. See `RELEASE-NOTES.md`.

## Verification and boundaries

Media tests cover real encoding, cancellation, recovery backups, source changes, nested streaming batches, rotation, 10-bit SDR, animation safeguards, basic/full validation, hardware fallback, measured encoder ranking and before/after size semantics. Development checks run on Linux; real Windows GPU speed, WPF appearance and installer execution require Windows testing. No pause/restart resume. Automatic quality-loss percentages and fixed speed/size guarantees are not provided.

Application source is MIT. FFmpeg and .NET retain their own licenses; review [THIRD-PARTY.md](THIRD-PARTY.md) for binary redistribution. No telemetry, accounts, media uploads or update service.
