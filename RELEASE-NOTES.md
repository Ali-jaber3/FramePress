# FramePress v0.1.1

- Clean replacement: keeps the base name, uses MP4/WebP, and deletes the original only after a smaller output passes full decoding. No persistent media backup or transaction file is created.
- Existing sibling targets are never overwritten. Same-extension replacement uses File.Replace without a backup. Changing extensions publishes the verified result before deleting the source; an interruption in that small interval can leave both files for manual review.
- Predictable copy names: name.framepress-compressed.mp4 (numbered on collision). Temporary samples are deleted after measurement.
- Stops encoding when reported output bytes reach the original size. Checks final size again before verification. Larger outputs are discarded; original retained. No automatic reduction of quality behind the user's back.
- Shorter encoder benchmarks (up to 1 second / 24 frames), cached known hardware initialization failures during the session, and faster WebP effort (4 instead of 6). Actual speed depends on the media and hardware; full decode remains mandatory for replacement.
- Arabic preset names, clearer CPU/GPU choices, CRF explanation, and corrected scan/manual/sample wording.
- Select a video for an estimate directly without a folder scan. Shows time for that file, a one-hour example, predicted full size and measured sample SSIM. Quality measurement time is excluded from the encode estimate. Replacement full verification time is additional.
- SSIM is a structural similarity index, NOT a percentage of perceived quality retained/lost; one sample is not a guarantee for the full video. No invented CRF-to-loss percentage.
- Carries forward WPF StartupUri and binding-log fixes and NSIS uninstall quoting fix.

Target: Windows 11 x64. Download FramePress-v0.1.1-Setup.exe. .NET and FFmpeg are bundled by the Windows build.

New help text is localized for Arabic and English. Other existing language dictionaries remain available; changed safety/help text currently uses English fallback.
