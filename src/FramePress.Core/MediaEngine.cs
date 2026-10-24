using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FramePress.Core;

public sealed class MediaEngine
{
    private readonly EncoderPolicy _encoderPolicy;
    public event Action<EncoderMeasurement>? EncoderMeasured;
    public string Ffmpeg { get; }
    public string Ffprobe { get; }
    public static HashSet<string> VideoExtensions { get; } = new(StringComparer.OrdinalIgnoreCase)
        { ".mp4", ".mov", ".mkv", ".avi", ".mxf", ".mts", ".m2ts", ".m4v", ".webm", ".wmv", ".mpg", ".mpeg" };
    public static HashSet<string> ImageExtensions { get; } = new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp" };
    public MediaEngine(string? binaryFolder = null)
    {
        var folder = binaryFolder ?? System.IO.Path.Combine(AppContext.BaseDirectory, "tools");
        string Resolve(string name)
        {
            var ext = OperatingSystem.IsWindows() ? ".exe" : "";
            var bundled = System.IO.Path.Combine(folder, name + ext);
            return File.Exists(bundled) ? bundled : name + ext;
        }
        Ffmpeg = Resolve("ffmpeg"); Ffprobe = Resolve("ffprobe"); _encoderPolicy = new(this);
    }
    public async Task VerifyToolsAsync(CancellationToken token)
    {
        foreach (var file in new[] { Ffmpeg, Ffprobe })
        {
            var r = await ProcessRunner.RunAsync(file, ["-version"], token);
            if (r.ExitCode != 0) throw new IOException("FFmpeg tools unavailable: " + file);
        }
        var encoders = await ProcessRunner.RunAsync(Ffmpeg, ["-hide_banner", "-encoders"], token);
        if (!encoders.Output.Contains("libx264")) throw new IOException("FFmpeg build needs libx264.");
    }
    public static string Classify(int w, int h)
    {
        var shortEdge = Math.Min(w, h); var longEdge = Math.Max(w, h);
        foreach (var (tier, small, large) in new[] { ("8K+", 4320, 7680), ("4K", 2160, 3840), ("1440p", 1440, 2560), ("1080p", 1080, 1920), ("720p", 720, 1280) })
            if (shortEdge >= small || longEdge >= large) return tier;
        return "SD";
    }
    public static IEnumerable<string> Discover(string root, bool recursive, Action<string>? warning = null)
    {
        var pending = new Stack<string>(); pending.Push(System.IO.Path.GetFullPath(root));
        while (pending.TryPop(out var dir))
        {
            string[] entries;
            try { entries = Directory.GetFileSystemEntries(dir); }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException) { warning?.Invoke(dir + ": " + e.Message); continue; }
            foreach (var entry in entries)
            {
                FileAttributes attrs;
                try { attrs = File.GetAttributes(entry); }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException) { warning?.Invoke(entry + ": " + e.Message); continue; }
                if (attrs.HasFlag(FileAttributes.ReparsePoint)) continue;
                if (attrs.HasFlag(FileAttributes.Directory))
                {
                    if (recursive && !System.IO.Path.GetFileName(entry).StartsWith(".framepress", StringComparison.OrdinalIgnoreCase)) pending.Push(entry);
                    continue;
                }
                var name = System.IO.Path.GetFileName(entry);
                if (name.Contains(".framepress-", StringComparison.OrdinalIgnoreCase) || name.StartsWith(".framepress", StringComparison.OrdinalIgnoreCase)) continue;
                var extension = System.IO.Path.GetExtension(entry);
                if (VideoExtensions.Contains(extension) || ImageExtensions.Contains(extension)) yield return entry;
            }
        }
    }
    public async Task<MediaInfo> ProbeAsync(string path, CancellationToken token)
    {
        if (ImageExtensions.Contains(System.IO.Path.GetExtension(path))) ImageSafety.EnsureStatic(path);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(45));
        ProcessResult result;
        try { result = await ProcessRunner.RunAsync(Ffprobe, ["-v", "error", "-show_streams", "-show_format", "-of", "json", path], timeout.Token); }
        catch (OperationCanceledException) when (!token.IsCancellationRequested) { throw new IOException("ffprobe timeout: " + path); }
        if (result.ExitCode != 0) throw new IOException(result.Error);
        using var doc = JsonDocument.Parse(result.Output);
        var streams = doc.RootElement.GetProperty("streams").EnumerateArray().ToArray();
        static string Str(JsonElement e, string key) => e.TryGetProperty(key, out var p) ? p.ToString() : "";
        static int Int(JsonElement e, string key) => int.TryParse(Str(e, key), out var n) ? n : 0;
        var videos = streams.Where(s => Str(s, "codec_type") == "video" &&
            !(s.TryGetProperty("disposition", out var d) && Int(d, "attached_pic") == 1)).ToArray();
        if (videos.Length == 0) throw new IOException("No video/image stream.");
        var v = videos[0]; var image = ImageExtensions.Contains(System.IO.Path.GetExtension(path));
        var duration = Str(v, "duration");
        if (doc.RootElement.TryGetProperty("format", out var f) && Str(f, "duration") is { Length: > 0 } fd) duration = fd;
        double.TryParse(duration, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds);
        if (!image && seconds <= 0) throw new IOException("Missing video duration.");
        if (image && int.TryParse(Str(v, "nb_frames"), out var count) && count > 1) throw new IOException("Animated images are not supported.");
        var width = Int(v, "width"); var height = Int(v, "height");
        if (width <= 0 || height <= 0) throw new IOException("Invalid dimensions.");
        var rotation = "0";
        if (v.TryGetProperty("side_data_list", out var side))
            foreach (var s in side.EnumerateArray()) if (s.TryGetProperty("rotation", out var r)) rotation = r.ToString();
        var info = new FileInfo(path);
        return new(path, image, width, height, seconds, info.Length, info.LastWriteTimeUtc,
            image ? "Images" : Classify(width, height), Str(v, "pix_fmt"), Str(v, "color_transfer"), Str(v, "color_primaries"), Str(v, "color_space"), videos.Length,
            streams.Count(s => Str(s, "codec_type") == "audio"), streams.Any(s => Str(s, "codec_type") == "subtitle"),
            streams.Any(s => Str(s, "codec_type") == "data"), rotation);
    }
    public static void ValidateMedia(MediaInfo m, CompressionProfile p, JobOptions o)
    {
        p.Validate();
        if (o.ImageQuality is < 50 or > 100) throw new ArgumentException("Image quality must be 50–100.");
        if (o.Sample && m.IsImage) throw new ArgumentException("Select a video for sampling.");
        if (m.VideoStreams != 1 || m.HasSubtitles) throw new IOException("Review required: multiple video streams or subtitles.");
        if (m.HasData && o.Replace && !o.Sample) throw new IOException("Copy mode required: professional data/timecode streams are not preserved.");
        if (m.IsHdr) throw new IOException("HDR skipped: this release does not implement verified HDR metadata preservation or tone mapping.");
        if (!m.IsImage && m.IsHighBitDepth && (!o.AllowHighBitDepth || p.Codec != "H.265"))
            throw new IOException("10-bit+ source: use H.265 and enable 10-bit SDR output, or keep the camera master.");
    }
    public List<string> BuildCommand(MediaInfo m, CompressionProfile p, JobOptions o, string temp, string? encoder = null)
    {
        ValidateMedia(m, p, o);
        var args = new List<string> { "-hide_banner", "-nostdin", "-v", "error", "-n" };
        if (o.Sample) args.AddRange(["-ss", Math.Max(0, m.Duration / 2 - 10).ToString("0.###", CultureInfo.InvariantCulture)]);
        args.AddRange(["-i", m.Path]);
        if (m.IsImage)
            args.AddRange(["-map", "0:v:0", "-frames:v", "1", "-c:v", "libwebp", "-quality", o.ImageQuality.ToString(CultureInfo.InvariantCulture), "-compression_level", "4"]);
        else
        {
            if (o.Sample) args.AddRange(["-t", "20"]);
            encoder ??= EncoderPolicy.Software(p.Codec);
            args.AddRange(["-map", "0:v:0", "-map", "0:a?", "-map_metadata", "0", "-map_chapters", "0"]);
            args.AddRange(EncoderPolicy.Arguments(encoder, p));
            args.AddRange(["-vf", "pad=ceil(iw/2)*2:ceil(ih/2)*2", "-pix_fmt", EncoderPolicy.PixelFormat(encoder, m.IsHighBitDepth),
                "-fps_mode", "passthrough", "-c:a", "aac", "-b:a", "192k", "-movflags", "+faststart"]);
            if (p.Codec == "H.265") args.AddRange(["-tag:v", "hvc1"]);
            foreach (var (key, val) in new[] { ("-color_trc", m.Transfer), ("-color_primaries", m.Primaries), ("-colorspace", m.ColorSpace) })
                if (!string.IsNullOrWhiteSpace(val) && val != "unknown") args.AddRange([key, val]);
        }
        args.AddRange(["-progress", "pipe:1", "-nostats", temp]); return args;
    }
    public static SampleEstimate Estimate(double elapsed, double encodedDuration, long sampleBytes, double fullDuration)
    {
        if (elapsed <= 0 || encodedDuration <= 0 || fullDuration <= 0) throw new ArgumentOutOfRangeException(nameof(encodedDuration));
        return new(elapsed / encodedDuration * 60, sampleBytes / encodedDuration * fullDuration, encodedDuration);
    }
    public static string OutputDirectory(MediaInfo m, JobOptions o)
    {
        if (o.Replace || string.IsNullOrWhiteSpace(o.OutputRoot)) return System.IO.Path.GetDirectoryName(m.Path)!;
        var relative = o.SourceRoot is null ? "" : System.IO.Path.GetRelativePath(o.SourceRoot, System.IO.Path.GetDirectoryName(m.Path)!);
        if (System.IO.Path.IsPathRooted(relative) || relative == ".." || relative.StartsWith(".." + System.IO.Path.DirectorySeparatorChar))
            throw new IOException("Output path would escape the chosen destination.");
        return System.IO.Path.GetFullPath(System.IO.Path.Combine(o.OutputRoot, relative));
    }
    public async Task<JobResult> ProcessAsync(MediaInfo media, CompressionProfile profile, JobOptions options,
        IProgress<JobProgress>? progress, CancellationToken token)
    {
        ValidateMedia(media, profile, options); token.ThrowIfCancellationRequested();
        var directory = OutputDirectory(media, options); Directory.CreateDirectory(directory);
        var suffix = media.IsImage ? ".webp" : ".mp4"; var id = Guid.NewGuid().ToString("N")[..10];
        var temp = System.IO.Path.Combine(directory, $".framepress-work-{id}{suffix}");
        var stem = System.IO.Path.GetFileNameWithoutExtension(media.Path);
        var output = options.Replace && !options.Sample
            ? OutputCommitter.ReplacementPath(media.Path, suffix)
            : System.IO.Path.Combine(directory, $"{stem}.framepress-compressed{suffix}");
        if (options.Replace && !options.Sample) OutputCommitter.EnsureTargetAvailable(media.Path, output);
        else
        {
            var number = 2;
            while (File.Exists(output)) output = System.IO.Path.Combine(directory, $"{stem}.framepress-compressed-{number++}{suffix}");
        }
        var clock = Stopwatch.StartNew(); var backup = "";
        var expectedSeconds = options.Sample ? Math.Min(20, media.Duration - Math.Max(0, media.Duration / 2 - 10)) : media.Duration;
        try
        {
            var drive = new DriveInfo(System.IO.Path.GetPathRoot(directory)!);
            if (drive.IsReady && drive.AvailableFreeSpace < Math.Min(media.Bytes, 512L * 1024 * 1024)) throw new IOException("Insufficient free space.");
            var fullVerification = !options.Sample && (options.FullVerification || options.Replace);
            progress?.Report(new(0, "SelectingEncoder", 0));
            var selectionStarted = clock.Elapsed.TotalSeconds;
            var encoder = media.IsImage ? "libwebp" : await _encoderPolicy.ResolveAsync(options.Acceleration, profile, media.IsHighBitDepth, token, media, m => EncoderMeasured?.Invoke(m));
            var selectionSeconds = clock.Elapsed.TotalSeconds - selectionStarted;
            var fellBack = false;
            progress?.Report(new(0, "Encoding", 0, encoder));
            void OnLine(string line)
            {
                if (line.StartsWith("out_time_us=", StringComparison.Ordinal) && double.TryParse(line[12..], NumberStyles.Float, CultureInfo.InvariantCulture, out var us))
                    progress?.Report(new(Math.Clamp(us / 1e6 / Math.Max(1, expectedSeconds), 0, .98) * (fullVerification ? .85 : .95), "Encoding", us / 1e6, encoder));
            }
            var encoded = await EncodeWithinBudgetAsync(BuildCommand(media, profile, options, temp, encoder), options.Sample ? null : media.Bytes, token, OnLine);
            if (encoded is null) return new(media.Path, "", "", "LargerSkipped", media.Bytes, media.Bytes, clock.Elapsed.TotalSeconds, "Encoding stopped when output reached original size; original retained.");
            if (encoded.ExitCode != 0 && !media.IsImage && !encoder.StartsWith("lib", StringComparison.Ordinal))
            {
                token.ThrowIfCancellationRequested();
                if (File.Exists(temp)) File.Delete(temp);
                _encoderPolicy.Disable(options.Acceleration, profile, media.IsHighBitDepth, media);
                encoder = EncoderPolicy.Software(profile.Codec); fellBack = true;
                progress?.Report(new(0, "CpuFallback", 0));
                encoded = await EncodeWithinBudgetAsync(BuildCommand(media, profile, options, temp, encoder), options.Sample ? null : media.Bytes, token, OnLine);
                if (encoded is null) return new(media.Path, "", "", "LargerSkipped", media.Bytes, media.Bytes, clock.Elapsed.TotalSeconds, "Encoding stopped when output reached original size; original retained.");
            }
            if (encoded.ExitCode != 0) throw new IOException(encoded.Error);
            var bytes = new FileInfo(temp).Length;
            if (bytes == 0) throw new IOException("Empty output.");
            if (!options.Sample && bytes >= media.Bytes)
                return new(media.Path, "", "", "LargerSkipped", media.Bytes, media.Bytes, clock.Elapsed.TotalSeconds,
                    "Output was not smaller; temporary output deleted and original retained. Try a higher quality-number or H.265; test a sample first.");
            progress?.Report(new(fullVerification ? .86 : .96, "Verifying", expectedSeconds, encoder));
            var check = await ProbeAsync(temp, token);
            var rotated = double.TryParse(media.Rotation, NumberStyles.Float, CultureInfo.InvariantCulture, out var angle) && Math.Abs(angle % 180) > 89 && Math.Abs(angle % 180) < 91;
            var expectedWidth = rotated ? media.Height : media.Width;
            var expectedHeight = rotated ? media.Width : media.Height;
            if (!media.IsImage)
            {
                expectedWidth += expectedWidth % 2; expectedHeight += expectedHeight % 2;
                if (Math.Abs(check.Duration - expectedSeconds) > Math.Max(.75, expectedSeconds * .005) || check.AudioStreams != media.AudioStreams || check.Width != expectedWidth || check.Height != expectedHeight)
                    throw new IOException("Output duration/audio/dimensions verification failed.");
            }
            else if (check.Width != expectedWidth || check.Height != expectedHeight)
                throw new IOException("Output image dimensions verification failed.");
            if (fullVerification)
            {
                var decode = await ProcessRunner.RunAsync(Ffmpeg, ["-v", "error", "-xerror", "-nostdin", "-i", temp, "-map", "0:v:0", "-map", "0:a?", "-f", "null", "-"], token);
                if (decode.ExitCode != 0) throw new IOException("Output decode failed: " + decode.Error);
            }
            token.ThrowIfCancellationRequested();

            var now = new FileInfo(media.Path);
            if (now.Length != media.Bytes || now.LastWriteTimeUtc != media.LastWriteUtc) throw new IOException("Source changed since scan. Scan again.");
            var encodeSeconds = Math.Max(.001, clock.Elapsed.TotalSeconds - selectionSeconds);
            double? similarity = null;
            var qualityNote = "";
            if (options.Sample)
            {
                progress?.Report(new(.98, "MeasuringQuality", expectedSeconds, encoder));
                try { similarity = await MeasureSampleSsimAsync(media, temp, expectedSeconds, token); }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
                catch (Exception error) when (error is IOException or OperationCanceledException)
                { qualityNote = " SSIM unavailable: " + error.Message; }
                // Sample files are temporary; only the estimate and measurement survive.
                output = "";
            }
            else if (options.Replace) OutputCommitter.Replace(temp, media.Path, output);
            else File.Move(temp, output, overwrite: false);
            clock.Stop(); progress?.Report(new(1, "Done", expectedSeconds));
            var estimate = options.Sample ? Estimate(encodeSeconds, expectedSeconds, bytes, media.Duration) : null;
            var detail = $"Encoder: {encoder}. Validation: {(fullVerification ? "probe + full decode" : "probe only")}." + qualityNote;
            if (fellBack) detail += " Hardware encode failed; retried successfully on CPU.";
            if (media.HasData) detail += " Camera data/timecode streams omitted; original must be retained.";
            return new(media.Path, output, backup, options.Sample ? "Sample" : "Done", media.Bytes, bytes, clock.Elapsed.TotalSeconds, detail, estimate?.HourMinutes, estimate?.FullBytes, similarity);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
    private async Task<ProcessResult?> EncodeWithinBudgetAsync(List<string> args, long? budget,
        CancellationToken token, Action<string> progress)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var tooLarge = false;
        try
        {
            return await ProcessRunner.RunAsync(Ffmpeg, args, stop.Token, line =>
            {
                if (budget is { } limit && line.StartsWith("total_size=", StringComparison.Ordinal)
                    && long.TryParse(line[11..], NumberStyles.Integer, CultureInfo.InvariantCulture, out var size) && size >= limit)
                { tooLarge = true; stop.Cancel(); }
                progress(line);
            });
        }
        catch (OperationCanceledException) when (tooLarge && !token.IsCancellationRequested) { return null; }
    }
    public static double? ParseSsim(string text)
    {
        var matches = Regex.Matches(text, @"SSIM.*?All:([0-9]+(?:\.[0-9]+)?)");
        return matches.Count > 0 && double.TryParse(matches[^1].Groups[1].Value, NumberStyles.Float,
            CultureInfo.InvariantCulture, out var score) && score is >= 0 and <= 1 ? score : null;
    }
    public async Task<double?> MeasureSampleSsimAsync(MediaInfo media, string sample, double duration, CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(90));
        var offset = Math.Max(0, media.Duration / 2 - 10).ToString("0.###", CultureInfo.InvariantCulture);
        var format = media.IsHighBitDepth ? "yuv420p10le" : "yuv420p";
        var normalize = $"pad=ceil(iw/2)*2:ceil(ih/2)*2,format={format},settb=AVTB,setpts=PTS-STARTPTS";
        var filter = $"[0:v:0]{normalize}[ref];[1:v:0]{normalize}[out];[ref][out]ssim=shortest=1";
        var result = await ProcessRunner.RunAsync(Ffmpeg, ["-hide_banner", "-nostdin", "-v", "info", "-ss", offset,
            "-i", media.Path, "-i", sample, "-filter_complex", filter, "-an", "-t",
            duration.ToString("0.###", CultureInfo.InvariantCulture), "-f", "null", "-"], timeout.Token);
        if (result.ExitCode != 0) throw new IOException(result.Error);
        return ParseSsim(result.Error);
    }
    public static void ExportCsv(IEnumerable<JobResult> rows, string path)
    {
        static string Cell(string value)
        {
            if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
        using var writer = new StreamWriter(path, false, new UTF8Encoding(true));
        writer.WriteLine("Source,Output,Backup,State,InputBytes,OutputBytes,ElapsedSeconds,Detail");
        foreach (var r in rows)
            writer.WriteLine(string.Join(',', new[] { r.Source, r.Output, r.Backup, r.State,
                r.InputBytes.ToString(CultureInfo.InvariantCulture), r.OutputBytes.ToString(CultureInfo.InvariantCulture),
                r.ElapsedSeconds.ToString("0.###", CultureInfo.InvariantCulture), r.Detail }.Select(Cell)));
    }
}
