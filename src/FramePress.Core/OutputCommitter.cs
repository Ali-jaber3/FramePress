namespace FramePress.Core;

/// <summary>Publishes only validated smaller outputs. Never overwrites a sibling media file.</summary>
public static class OutputCommitter
{
    public static string ReplacementPath(string source, string extension) => Path.ChangeExtension(source, extension);
    public static void EnsureTargetAvailable(string source, string output)
    {
        if (!Path.GetFullPath(source).Equals(Path.GetFullPath(output),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal)
            && File.Exists(output))
            throw new IOException("Destination already exists; original retained: " + output);
    }
    public static void Replace(string temporary, string source, string output)
    {
        EnsureTargetAvailable(source, output);
        // Flush completed media before publishing. All files are siblings on the same volume.
        using (var stream = new FileStream(temporary, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) stream.Flush(true);
        if (Path.GetFullPath(source).Equals(Path.GetFullPath(output),
            OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
        {
            File.Replace(temporary, source, null);
            return;
        }
        // For a changed extension publish first. A crash in this short interval leaves both copies,
        // never a missing original. No persistent backup or transaction file is needed.
        File.Move(temporary, output, false);
        try { File.Delete(source); }
        catch
        {
            if (File.Exists(source)) File.Delete(output);
            throw;
        }
    }
}
