# Verification record — 24 October 2026 (Bahrain)

## Executed in this development environment

- .NET SDK: 10.0.402 on Linux x64.
- FFmpeg: 6.1.1, with libx264, libx265 and libwebp.
- WPF application Release build: **succeeded, zero warnings, zero errors**.
- `win-x64` self-contained publication: **succeeded**; application host verified as a PE32+ Windows GUI x86-64 executable.
- Readable creator executed with PowerShell 7.4.6: **44 source/document/artwork files created and compared with the authored project**. All four PowerShell scripts parsed successfully.
- Final standard integration suite: **27 passed, zero failed**.
- Earlier optional batch integration: **100 short synthetic videos encoded successfully; 100 source files retained**; nested source directories and Arabic names; measured encode batch 28.68 seconds in this environment. This is not a speed estimate for camera footage.

Standard checks include real H.264 video+audio, silent video, MOV→MP4 replacement, MP4 same-path replacement, recovery bytes, cancellation before/during work, changed-source rejection, nested output paths, path escape prevention, HDR guards, high-bit-depth guards, actual 10-bit SDR H.265, portrait dimensions, display-matrix rotation, WebP images, larger-output discard, animated PNG/WebP rejection, sample estimates, CSV escaping and language/XAML structure checks.

## Still requires Windows execution

- Real WPF startup/render/binding smoke test (provided in workflow, not executed here).
- Visual inspection, keyboard navigation, Arabic font rendering, DPI and window sizing.
- NSIS installer compilation/execution, shortcuts, upgrade/reinstall and uninstall on Windows.
- Clean account without preinstalled .NET/FFmpeg.
- Actual professional camera sources, long-file sync, editor import, grading and export.
- 100 long or mixed-resolution real videos, disk pressure, locked files, network shares and interrupted replacement recovery.

The result is an editable, compiled implementation with tested media logic, not a claim of production certification or complete testing on the user's Windows machine.

## Public v0.1.0 update
37 standard integration tests passed after adding device inventory, actual source-sample encoder comparisons, cache isolation and real after-size/reduction semantics. Windows x64 self-contained publication succeeded again. The tested Linux environment selected CPU; no physical Windows GPU benchmark or installer execution was performed here. Names are obtained from Windows CIM at runtime.
