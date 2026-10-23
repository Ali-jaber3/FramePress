# Third-party components

- .NET 10 / WPF: MIT application framework with third-party notices. The self-contained publish output includes its runtime notices; keep them with the package.
- FFmpeg: license depends on build configuration. libx264/libx265 builds usually introduce GPL requirements. The readable project source does not include FFmpeg binaries. Before distributing installers, include the selected build's license texts, configuration, notices and corresponding source as required. `licenses/` at the repository root is copied into the application package.
- NSIS: separate installer-tool license; preserve relevant notices if redistributing the tool itself.

The MIT license covers FramePress code, not third-party executables. The Windows workflow fetches dependencies for building/testing; it does not complete the FFmpeg redistribution compliance work for you.

References: https://ffmpeg.org/legal.html ; https://github.com/dotnet/runtime ; https://github.com/dotnet/wpf ; https://nsis.sourceforge.io/License
