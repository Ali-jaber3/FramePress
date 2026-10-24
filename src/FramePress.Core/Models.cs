using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;

namespace FramePress.Core;

public abstract class Observable : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    protected void Raise([CallerMemberName] string? name = null) => PropertyChanged?.Invoke(this, new(name));
    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value; Raise(name); return true;
    }
}

public sealed class CompressionProfile : Observable
{
    private bool _enabled = true;
    private int _crf = 20;
    private string _preset = "veryfast", _codec = "H.264", _sampleSummary = "";
    public string Tier { get; set; } = "1080p";
    public bool Enabled { get => _enabled; set => Set(ref _enabled, value); }
    public int Crf { get => _crf; set { if (Set(ref _crf, value)) { Raise(nameof(QualityHint)); SampleSummary = ""; } } }
    public string Preset { get => _preset; set { if (Set(ref _preset, value)) SampleSummary = ""; } }
    public string Codec { get => _codec; set { if (Set(ref _codec, value)) SampleSummary = ""; } }
    [JsonIgnore]
    public string SampleSummary { get => _sampleSummary; set => Set(ref _sampleSummary, value); }
    public string QualityHint => Crf <= 20 ? "16–20 · High quality / جودة مرتفعة" : Crf <= 23 ? "21–23 · Balanced / متوازنة" : "24–30 · More visible loss / فقد أوضح للتفاصيل";
    public CompressionProfile Snapshot() => new() { Tier = Tier, Enabled = Enabled, Crf = Crf, Preset = Preset, Codec = Codec };
    public void Validate()
    {
        if (Crf is < 16 or > 30) throw new ArgumentException("CRF must be between 16 and 30.");
        if (!Presets.Contains(Preset)) throw new ArgumentException("Unknown speed preset.");
        if (!Codecs.Contains(Codec)) throw new ArgumentException("Unknown video codec.");
    }
    public static string[] Tiers { get; } = ["SD", "720p", "1080p", "1440p", "4K", "8K+"];
    public static string[] Presets { get; } = ["ultrafast", "veryfast", "fast", "medium", "slow", "slower"];
    public static string[] Codecs { get; } = ["H.264", "H.265"];
}

public sealed record MediaInfo(string Path, bool IsImage, int Width, int Height, double Duration,
    long Bytes, DateTime LastWriteUtc, string Tier, string PixelFormat, string Transfer,
    string Primaries, string ColorSpace, int VideoStreams, int AudioStreams, bool HasSubtitles,
    bool HasData, string Rotation)
{
    public bool IsHdr => Transfer is "smpte2084" or "arib-std-b67";
    public bool IsHighBitDepth => PixelFormat.Contains("10") || PixelFormat.Contains("12") || PixelFormat.Contains("16");
    public string Dimensions => $"{Width} × {Height}";
    public string KindHint => IsImage ? "Image" : IsHdr ? "HDR · review" : IsHighBitDepth ? "10-bit+" : "SDR";
}

public sealed class QueueItem(MediaInfo media) : Observable
{
    private bool _selected = true;
    private long? _outputBytes;
    private double _progress;
    private string _state = "Ready", _detail = "", _output = "";
    public MediaInfo Media { get; } = media;
    public bool Selected { get => _selected; set => Set(ref _selected, value); }
    public string Name => System.IO.Path.GetFileName(Media.Path);
    public string Folder => System.IO.Path.GetDirectoryName(Media.Path) ?? "";
    public string Tier => Media.Tier;
    public string Dimensions => Media.Dimensions;
    public string Size => Display.Bytes(Media.Bytes);
    public string AfterSize => _outputBytes is { } bytes ? Display.Bytes(bytes) : "—";
    public string Reduction => _outputBytes is { } bytes && Media.Bytes > 0
        ? ((1 - bytes / (double)Media.Bytes) * 100).ToString("0.#", CultureInfo.InvariantCulture) + "%" : "—";
    public void RecordOutput(JobResult result)
    {
        // A 20-second sample is never presented as the full video's final size.
        if (result.State != "Done" || string.IsNullOrWhiteSpace(result.Output)) return;
        _outputBytes = result.OutputBytes; Raise(nameof(AfterSize)); Raise(nameof(Reduction));
    }
    public string Duration => Media.IsImage ? "—" : TimeSpan.FromSeconds(Media.Duration).ToString(@"hh\:mm\:ss");
    public string Kind => Media.KindHint;
    public double Progress { get => _progress; set => Set(ref _progress, value); }
    public string State { get => _state; set => Set(ref _state, value); }
    public string Detail { get => _detail; set => Set(ref _detail, value); }
    public string Output { get => _output; set => Set(ref _output, value); }
    public bool Completed { get; set; }
}

public sealed record JobOptions(bool Replace, string? OutputRoot, string? SourceRoot,
    int ImageQuality = 85, bool Sample = false, bool AllowHighBitDepth = false,
    string Acceleration = "CPU", bool FullVerification = true);
public sealed record JobProgress(double Fraction, string Stage, double EncodedSeconds, string Encoder = "");
public sealed record JobResult(string Source, string Output, string Backup, string State,
    long InputBytes, long OutputBytes, double ElapsedSeconds, string Detail,
    double? HourMinutes = null, double? EstimatedFullBytes = null, double? SampleSsim = null);
public sealed record SampleEstimate(double HourMinutes, double FullBytes, double SampleSeconds);

public static class Display
{
    public static string Bytes(double value)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        var i = 0; while (value >= 1024 && i < units.Length - 1) { value /= 1024; i++; }
        return value.ToString("0.##", CultureInfo.InvariantCulture) + " " + units[i];
    }
}
