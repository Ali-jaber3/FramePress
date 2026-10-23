using System.Runtime.InteropServices;
using System.Text.Json;

namespace FramePress.Core;

public sealed record HardwareSnapshot(string CpuName, string[] GpuNames, int LogicalProcessors, string Note = "");
public static class HardwareInfo
{
    public static async Task<HardwareSnapshot> ReadAsync(CancellationToken token)
    {
        var fallback = new HardwareSnapshot($"{RuntimeInformation.ProcessArchitecture} CPU", [], Environment.ProcessorCount);
        if (!OperatingSystem.IsWindows()) return fallback with { Note = "Windows CIM device inventory is unavailable on this platform." };
        // Names are informational. Encoder choice is based on a real FFmpeg benchmark,
        // never guessed from a GPU product name or advertised VRAM amount.
        const string script = "[Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false); " +
            "$ErrorActionPreference='Stop'; $cpu=(Get-CimInstance Win32_Processor | Select-Object -First 1).Name; " +
            "$gpu=@(Get-CimInstance Win32_VideoController | ForEach-Object { $_.Name } | Where-Object { $_ } | Select-Object -Unique); " +
            "[pscustomobject]@{CpuName=$cpu;GpuNames=$gpu;LogicalProcessors=[Environment]::ProcessorCount} | ConvertTo-Json -Compress";
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            var result = await ProcessRunner.RunAsync("powershell.exe", ["-NoProfile", "-NonInteractive", "-Command", script], timeout.Token);
            if (result.ExitCode != 0) return fallback with { Note = result.Error };
            return JsonSerializer.Deserialize<HardwareSnapshot>(result.Output) ?? fallback;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { return fallback with { Note = "Device inventory timed out." }; }
        catch (Exception e) when (e is System.ComponentModel.Win32Exception or JsonException or IOException)
        { return fallback with { Note = e.Message }; }
    }
}
