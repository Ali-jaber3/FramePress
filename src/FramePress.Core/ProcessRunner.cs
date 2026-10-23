using System.Diagnostics;
using System.Text;

namespace FramePress.Core;

public sealed record ProcessResult(int ExitCode, string Output, string Error);
public static class ProcessRunner
{
    public static async Task<ProcessResult> RunAsync(string file, IEnumerable<string> args,
        CancellationToken token, Action<string>? lineReceived = null)
    {
        token.ThrowIfCancellationRequested();
        var start = new ProcessStartInfo(file)
        {
            UseShellExecute = false, CreateNoWindow = true,
            RedirectStandardOutput = true, RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var arg in args) start.ArgumentList.Add(arg);
        using var process = new Process { StartInfo = start };
        if (!process.Start()) throw new IOException("Failed to start " + file);
        using var registration = token.Register(() =>
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
            catch (System.ComponentModel.Win32Exception) { }
        });
        var output = new StringBuilder(); var errors = new StringBuilder();
        static void AppendBounded(StringBuilder b, string s)
        {
            b.AppendLine(s);
            if (b.Length > 131072) b.Remove(0, b.Length - 131072);
        }
        var stdout = Task.Run(async () =>
        {
            while (await process.StandardOutput.ReadLineAsync() is { } line)
            {
                if (lineReceived is null) AppendBounded(output, line);
                else lineReceived(line);
            }
        }, CancellationToken.None);
        var stderr = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync() is { } line) AppendBounded(errors, line);
        }, CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
            await Task.WhenAll(stdout, stderr).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return new(process.ExitCode, output.ToString(), errors.ToString());
        }
        finally
        {
            try { if (!process.HasExited) process.Kill(entireProcessTree: true); }
            catch (InvalidOperationException) { }
        }
    }
}
