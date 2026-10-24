using System.Collections.Concurrent;
using System.Globalization;
using System.Diagnostics;

namespace FramePress.Core;

public sealed record EncoderMeasurement(string Encoder, double Seconds, int Frames, bool Success, string Detail)
{
    public double FramesPerSecond => Success && Seconds > 0 ? Frames / Seconds : 0;
}

public sealed class EncoderPolicy(MediaEngine engine)
{
    private readonly ConcurrentDictionary<string, string> _working = new();
    private readonly ConcurrentDictionary<string, byte> _unavailable = new();
    public static string[] Modes { get; } = ["Auto", "CPU", "NVIDIA", "Intel", "AMD"];
    public static IEnumerable<string> Candidates(string mode, string codec)
    {
        var prefix = codec == "H.265" ? "hevc" : "h264";
        if (mode is "Auto" or "NVIDIA") yield return prefix + "_nvenc";
        if (mode is "Auto" or "Intel") yield return prefix + "_qsv";
        if (mode is "Auto" or "AMD") yield return prefix + "_amf";
    }
    public static string Software(string codec) => codec == "H.265" ? "libx265" : "libx264";
    public static string PixelFormat(string encoder, bool tenBit) => encoder.StartsWith("lib", StringComparison.Ordinal)
        ? tenBit ? "yuv420p10le" : "yuv420p"
        : tenBit ? "p010le" : "nv12";
    public static List<string> Arguments(string encoder, CompressionProfile p)
    {
        var quality = p.Crf.ToString(CultureInfo.InvariantCulture);
        var args = new List<string> { "-c:v", encoder };
        if (encoder.EndsWith("_nvenc", StringComparison.Ordinal))
        {
            var preset = p.Preset switch { "ultrafast" => "p1", "veryfast" => "p2", "fast" => "p3", "medium" => "p4", "slow" => "p5", _ => "p6" };
            args.AddRange(["-preset", preset, "-tune", "hq", "-rc", "vbr", "-cq", quality, "-b:v", "0"]);
        }
        else if (encoder.EndsWith("_qsv", StringComparison.Ordinal))
            args.AddRange(["-preset", p.Preset == "ultrafast" ? "veryfast" : p.Preset, "-global_quality", quality]);
        else if (encoder.EndsWith("_amf", StringComparison.Ordinal))
            args.AddRange(["-quality", p.Preset is "ultrafast" or "veryfast" or "fast" ? "speed" : "balanced", "-rc", "cqp", "-qp_i", quality, "-qp_p", quality]);
        else args.AddRange(["-crf", quality, "-preset", p.Preset]);
        return args;
    }
    public static string CacheKey(string mode, CompressionProfile p, bool tenBit, MediaInfo? media) =>
        string.Join(":", mode, p.Codec, tenBit, media?.Width ?? 960, media?.Height ?? 540, p.Crf, p.Preset);

    public static string PickFastest(IEnumerable<EncoderMeasurement> measurements, string fallback) =>
        measurements.Where(m => m.Success && m.Frames > 0 && m.Seconds > 0)
            .OrderByDescending(m => m.FramesPerSecond).Select(m => m.Encoder).FirstOrDefault() ?? fallback;

    public async Task<string> ResolveAsync(string mode, CompressionProfile profile, bool tenBit, CancellationToken token,
        MediaInfo? media = null, Action<EncoderMeasurement>? measured = null)
    {
        if (!Modes.Contains(mode)) throw new ArgumentException("Unknown acceleration mode.");
        var software = Software(profile.Codec);
        if (mode == "CPU") return software;
        var key = CacheKey(mode, profile, tenBit, media);
        if (_working.TryGetValue(key, out var cached)) return cached;
        var candidates = Candidates(mode, profile.Codec).ToList();
        if (mode == "Auto") candidates.Add(software);
        var measurements = new List<EncoderMeasurement>();
        foreach (var candidate in candidates)
        {
            token.ThrowIfCancellationRequested();
            var unavailableKey = candidate + ":" + tenBit;
            if (_unavailable.ContainsKey(unavailableKey)) continue;
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
            timeout.CancelAfter(TimeSpan.FromSeconds(8));
            var probe = new List<string> { "-v", "error", "-nostdin" };
            if (media is not null)
                probe.AddRange(["-ss", Math.Max(0, media.Duration / 2 - 1).ToString("0.###", CultureInfo.InvariantCulture), "-i", media.Path]);
            else probe.AddRange(["-f", "lavfi", "-i", "testsrc2=size=960x540:rate=24"]);
            probe.AddRange(["-map", "0:v:0", "-t", "1", "-frames:v", "24", "-an", "-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2"]);
            probe.AddRange(Arguments(candidate, profile));
            probe.AddRange(["-pix_fmt", PixelFormat(candidate, tenBit), "-progress", "pipe:1", "-nostats", "-f", "null", "-"]);
            var watch = Stopwatch.StartNew(); var frames = 0;
            EncoderMeasurement measurement;
            try
            {
                var result = await ProcessRunner.RunAsync(engine.Ffmpeg, probe, timeout.Token, line =>
                {
                    if (line.StartsWith("frame=", StringComparison.Ordinal) && int.TryParse(line[6..], out var count)) frames = count;
                });
                measurement = new(candidate, watch.Elapsed.TotalSeconds, frames, result.ExitCode == 0 && frames > 0,
                    result.ExitCode == 0 ? "Short sample at source dimensions; includes initialization." : result.Error);
            }
            catch (OperationCanceledException) when (!token.IsCancellationRequested)
            { measurement = new(candidate, watch.Elapsed.TotalSeconds, frames, false, "Encoder benchmark timed out."); }
            if (!measurement.Success && new[] { "Cannot load", "No device", "No capable devices", "Unknown encoder", "Error loading", "unsupported (-3)" }
                .Any(message => measurement.Detail.Contains(message, StringComparison.OrdinalIgnoreCase)))
                _unavailable.TryAdd(unavailableKey, 0);
            measurements.Add(measurement); measured?.Invoke(measurement);
            if (mode != "Auto" && measurement.Success) { _working[key] = candidate; return candidate; }
        }
        // Fastest measured valid encoder at these settings. This is not a visual
        // quality score: CRF, CQ, and QP are not equivalent between encoders.
        var winner = PickFastest(measurements, software); _working[key] = winner; return winner;
    }
    public void Disable(string mode, CompressionProfile p, bool tenBit, MediaInfo? media = null) =>
        _working[CacheKey(mode, p, tenBit, media)] = Software(p.Codec);
}
