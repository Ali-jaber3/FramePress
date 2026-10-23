# Arabic and appearance repair

This repair addresses the reported Windows screenshot:

- Arabic and other Unicode text are reinstalled from clean UTF-8 sources.
- The repair script contains ASCII characters only and transports text as JSON Unicode escapes. It remains readable by Windows PowerShell 5.1 even when saved as UTF-8 without a BOM or a legacy ANSI encoding.
- Window background/foreground/font are set explicitly on MainWindow, avoiding reliance on the implicit base Window style.
- Language selector uses an explicit Name item template, avoiding record ToString in the selection box.
- Tab content and scrollbars use themed templates. The Windows 11 title bar requests the matching DWM dark/light appearance.
- Size summaries display their units left-to-right inside Arabic layout.
- The Windows smoke test now checks Arabic text, language selector rendering, RTL/LTR and actual window background for Arabic/English in both themes, and produces four screenshots. Runtime execution is still pending on Windows.

Close FramePress, run Fix-FramePress-UI.ps1 from the source project root, then rebuild/run. The script backs up affected source files in a dated .framepress-ui-backup-* directory before writing and attempts rollback on write failure. It does not change videos, FFmpeg tools or user preferences.

Local checks: WPF compilation and a repair round-trip against intentionally corrupted Arabic resources. These do not replace visual verification on Windows.
