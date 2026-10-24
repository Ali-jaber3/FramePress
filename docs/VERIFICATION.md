# v0.1.1 verification

Local checks: .NET 10 compilation with Windows targeting, console integration tests with real FFmpeg on Linux, including replacement/no backups, name collisions, cancellation, larger-output guards, actual SSIM sample/no sample remnants, encoder fallback, CSV and locale/XAML checks.

Windows WPF visual rendering, native GPU throughput, installer installation/uninstallation and camera/editing workflows must be checked on Windows. Cross-compilation is not proof of those checks. CI keeps the WPF smoke test enabled.

SSIM reference: https://ffmpeg.org/ffmpeg-filters.html#ssim
Same-path replace without backup: https://learn.microsoft.com/dotnet/api/system.io.file.replace

The automatic benchmark is a short source-dimension speed comparison, includes encoder initialization, and is not a quality comparison. No claim of a fixed percentage speedup or quality loss is made.
