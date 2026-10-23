# FramePress v0.1.0

First public release of FramePress, a native Windows batch video and image compressor.

- Search nested folders and compress eligible media in one action.
- Set quality, codec and speed independently per resolution.
- Detect CPU/GPU names and benchmark working hardware/software encoders automatically.
- Compare actual before/after sizes and saved percentage.
- Use Arabic or nine other languages, with light and dark themes.
- Keep originals or replace with recovery backups; export a CSV report.

Download **FramePress-v0.1.0-Setup.exe**, install it, then choose a folder and test a short clip first. The installer bundles .NET and FFmpeg; end users do not need VS Code or the SDK.

Known limits: HDR is skipped; automatic encoder selection measures short-sample speed, not objective visual quality. Some camera metadata is not preserved. GPU speed depends on codec, device and driver. Keep camera masters and compare samples in your editing application. See README and THIRD-PARTY for details.
