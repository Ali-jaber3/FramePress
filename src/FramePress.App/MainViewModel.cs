using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using FramePress.Core;

namespace FramePress.App;
public sealed class MainViewModel : Observable
{
    private CancellationTokenSource? _cancel;
    private readonly MediaEngine _engine = new();
    private bool _busy, _dark, _recursive, _images, _highBit, _fullVerification, _indeterminate;
    private string _acceleration = "Auto";
    private bool _showAfterSize = true;
    private string _hardwareCpu = "", _hardwareGpu = "", _encoderSummary = "";
    private string _source = "", _destination = "", _status = "", _estimate = "", _current = "";
    private int _mode, _imageQuality;
    private double _overall;
    private LanguageOption _language;
    public Localizer L { get; }
    public ObservableCollection<QueueItem> Files { get; } = [];
    public ObservableCollection<CompressionProfile> Profiles { get; } = [];
    public ObservableCollection<JobResult> Results { get; } = [];
    public ObservableCollection<string> Log { get; } = [];
    public LanguageOption[] Languages => Localizer.Languages;
    public bool Busy { get => _busy; private set { Set(ref _busy, value); Raise(nameof(Idle)); } }
    public bool Idle => !Busy;
    public bool ShowAfterSize { get => _showAfterSize; set => Set(ref _showAfterSize, value); }
    public string HardwareCpu { get => _hardwareCpu; private set => Set(ref _hardwareCpu, value); }
    public string HardwareGpu { get => _hardwareGpu; private set => Set(ref _hardwareGpu, value); }
    public string EncoderSummary { get => _encoderSummary; private set => Set(ref _encoderSummary, value); }
    public string OutputSize => Display.Bytes(Results.Where(r => r.State == "Done").Sum(r => (double)r.OutputBytes));
    public ChoiceOption[] AccelerationModes => EncoderPolicy.Modes.Select(value => new ChoiceOption(value, L["Acceleration_" + value])).ToArray();
    public ChoiceOption[] SpeedChoices => CompressionProfile.Presets.Select(value => new ChoiceOption(value, L["Preset_" + value])).ToArray();
    public string Acceleration { get => _acceleration; set => Set(ref _acceleration, value); }
    public bool FullVerification { get => _fullVerification; set => Set(ref _fullVerification, value); }
    public bool Indeterminate { get => _indeterminate; private set => Set(ref _indeterminate, value); }
    public bool Dark { get => _dark; set => Set(ref _dark, value); }
    public bool Recursive { get => _recursive; set => Set(ref _recursive, value); }
    public bool Images { get => _images; set => Set(ref _images, value); }
    public bool AllowHighBitDepth { get => _highBit; set => Set(ref _highBit, value); }
    public int ImageQuality { get => _imageQuality; set => Set(ref _imageQuality, value); }
    public int Mode { get => _mode; set => Set(ref _mode, value); }
    public string Source { get => _source; set => Set(ref _source, value); }
    public string Destination { get => _destination; set => Set(ref _destination, value); }
    public string Status { get => _status; set => Set(ref _status, value); }
    public string Estimate { get => _estimate; set => Set(ref _estimate, value); }
    public string Current { get => _current; set => Set(ref _current, value); }
    public double Overall { get => _overall; set => Set(ref _overall, value); }
    public string CountText => Files.Count.ToString();
    public string TotalSize => Display.Bytes(Files.Sum(f => (double)f.Media.Bytes));
    public string SavedSize => Display.Bytes(Results.Where(r => r.State == "Done").Sum(r => (double)Math.Max(0, r.InputBytes - r.OutputBytes)));
    public LanguageOption Language
    {
        get => _language;
        set { if (Set(ref _language, value) && value is not null) { L.SetLanguage(value.Code); Raise(nameof(Direction)); Raise(nameof(AccelerationModes)); Raise(nameof(SpeedChoices)); } }
    }
    public FlowDirection Direction => L.Direction;
    public MainViewModel()
    {
        var p = Preferences.Load(); L = new(p.Language);
        _language = Languages.Single(l => l.Code == L.Code);
        _dark = p.Dark; _recursive = p.Recursive; _images = p.Images; _imageQuality = p.ImageQuality; _highBit = p.AllowHighBitDepth;
        _acceleration = p.Acceleration; _fullVerification = p.FullVerification; _showAfterSize = p.ShowAfterSize;
        HardwareCpu = L["DetectingHardware"]; HardwareGpu = L["DetectingHardware"]; EncoderSummary = "—";
        _engine.EncoderMeasured += m => AddLog($"Benchmark {m.Encoder}: {(m.Success ? m.FramesPerSecond.ToString("0.0") + " fps" : "unavailable")} · {m.Seconds:0.00}s · {m.Detail[..Math.Min(280, m.Detail.Length)]}");
        foreach (var tier in CompressionProfile.Tiers) Profiles.Add(p.Profiles.FirstOrDefault(v => v.Tier == tier) ?? new() { Tier = tier });
        Status = L["Ready"];
        Files.CollectionChanged += (_, _) => { Raise(nameof(CountText)); Raise(nameof(TotalSize)); };
        Results.CollectionChanged += (_, _) => { Raise(nameof(SavedSize)); Raise(nameof(OutputSize)); };
    }
    public void Save()
    {
        try { new Preferences { Language = Language.Code, Dark = Dark, Recursive = Recursive, Images = Images, ImageQuality = ImageQuality, AllowHighBitDepth = AllowHighBitDepth, Acceleration = Acceleration, FullVerification = FullVerification, ShowAfterSize = ShowAfterSize, SpeedUpgradeApplied = true, Profiles = Profiles.Select(p => p.Snapshot()).ToList() }.Save(); }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException) { AddLog("Settings: " + e.Message); }
    }
    public async Task LoadHardwareAsync()
    {
        try
        {
            var hardware = await HardwareInfo.ReadAsync(CancellationToken.None);
            HardwareCpu = hardware.CpuName + $" · {hardware.LogicalProcessors} threads";
            HardwareGpu = hardware.GpuNames.Length > 0 ? string.Join(" | ", hardware.GpuNames) : L["GpuUnknown"];
            AddLog("CPU: " + HardwareCpu); AddLog("GPU: " + HardwareGpu);
            if (!string.IsNullOrWhiteSpace(hardware.Note)) AddLog(hardware.Note);
        }
        catch (Exception error) { HardwareCpu = Environment.ProcessorCount + " logical CPUs"; HardwareGpu = L["GpuUnknown"]; AddLog(error.Message); }
    }
    private void AddLog(string text)
    {
        if (!Application.Current.Dispatcher.CheckAccess()) { Application.Current.Dispatcher.Invoke(() => AddLog(text)); return; }
        Log.Add(DateTime.Now.ToString("HH:mm:ss") + "  " + text);
        if (Log.Count > 500) Log.RemoveAt(0);
    }
    public void Cancel() { _cancel?.Cancel(); Status = L["Cancelling"]; }
    public void Clear()
    {
        if (Busy) return; Files.Clear(); Results.Clear(); Overall = 0; Estimate = ""; Current = ""; Status = L["Ready"];
    }
    public async Task RunFolderAsync()
    {
        if (Busy) return;
        if (!Directory.Exists(Source)) { Status = L["ChooseFolderFirst"]; return; }
        var root = Path.GetFullPath(Source); var recursive = Recursive; var images = Images;
        var profiles = Profiles.ToDictionary(p => p.Tier, p => p.Snapshot());
        try { foreach (var profile in profiles.Values) profile.Validate(); }
        catch (Exception error) { MessageBox.Show(error.Message, "FramePress"); return; }
        if (ImageQuality is < 50 or > 100) { MessageBox.Show("Image quality: 50–100", "FramePress"); return; }
        if (!images && !profiles.Values.Any(p => p.Enabled)) { Status = L["NoFiles"]; return; }
        if (Mode == 1 && MessageBox.Show(L["ReplaceWarning"], L["Confirm"], MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var options = new JobOptions(Mode == 1, string.IsNullOrWhiteSpace(Destination) ? null : Destination, root,
            ImageQuality, false, AllowHighBitDepth, Acceleration, FullVerification);
        Clear(); Busy = true; Indeterminate = true; _cancel = new(); Save(); Status = L["SearchingEncoding"];
        var rows = new Dictionary<string, QueueItem>(StringComparer.OrdinalIgnoreCase);
        var completed = 0;
        void Handle(BatchUpdate update)
        {
            rows.TryGetValue(update.Path, out var row);
            switch (update.Kind)
            {
                case "Inspecting": Current = Path.GetFileName(update.Path); break;
                case "Found":
                    row = new(update.Media!) { State = L["Ready"] }; rows[update.Path] = row; Files.Add(row); break;
                case "Progress" when row is not null:
                    row.Progress = update.Progress!.Fraction * 100; row.State = L[update.Progress.Stage];
                    if (!string.IsNullOrEmpty(update.Progress.Encoder))
                    { row.Detail = update.Progress.Encoder; EncoderSummary = update.Progress.Encoder; Current = row.Name + " · " + update.Progress.Encoder; }
                    break;
                case "Result":
                    var result = update.Result!; Results.Add(result); completed++;
                    if (row is not null)
                    { row.State = L[result.State]; row.Progress = 100; row.Detail = result.Detail; row.Output = result.Output; row.Completed = true; row.RecordOutput(result); }
                    AddLog(Path.GetFileName(update.Path) + " · " + result.State + " · " + result.Detail);
                    Status = L["SearchingEncoding"] + " · " + completed; break;
                case "Skipped": if (row is not null) { row.State = L["ProfileDisabled"]; row.Selected = false; } break;
                case "Cancelled": if (row is not null) row.State = L["Cancelled"]; break;
                case "Error":
                    if (row is not null) { row.State = L["Error"]; row.Detail = update.Detail; }
                    Results.Add(new(update.Path, "", "", "Error", row?.Media.Bytes ?? 0, row?.Media.Bytes ?? 0, 0, update.Detail));
                    AddLog(update.Path + ": " + update.Detail); break;
                case "Warning": AddLog(update.Detail); break;
            }
        }
        try
        {
            var token = _cancel.Token;
            await _engine.VerifyToolsAsync(token);
            var processor = new BatchProcessor(_engine);
            await Task.Run(() => processor.RunAsync(root, recursive, images, profiles, options,
                update => Application.Current.Dispatcher.Invoke(() => Handle(update)), token), token);
            Status = Files.Count == 0 ? L["NoFiles"] : L["Complete"];
            Overall = 100;
        }
        catch (OperationCanceledException) { Status = L["Cancelled"]; }
        catch (Exception error) { Status = L["Error"]; AddLog(error.Message); MessageBox.Show(error.Message, "FramePress"); }
        finally { Indeterminate = false; Busy = false; _cancel.Dispose(); _cancel = null; }
    }
    public async Task ScanAsync()
    {
        if (Busy || !Directory.Exists(Source)) { Status = L["ChooseFolderFirst"]; return; }
        var root = Path.GetFullPath(Source); var recursive = Recursive;
        Clear(); Busy = true; _cancel = new(); Status = L["Scanning"]; Save();
        try
        {
            await _engine.VerifyToolsAsync(_cancel.Token);
            var token = _cancel.Token;
            var found = new Progress<MediaInfo>(m => Files.Add(new(m) { State = L["Ready"] }));
            await Task.Run(async () =>
            {
                foreach (var path in MediaEngine.Discover(root, recursive, AddLog))
                {
                    token.ThrowIfCancellationRequested();
                    try { ((IProgress<MediaInfo>)found).Report(await _engine.ProbeAsync(path, token)); }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception e) { AddLog(path + ": " + e.Message); }
                }
            }, token);
            Status = L["ScanComplete"];
            AddLog($"Scan: {Files.Count} files; {Log.Count} log entries.");
            var transactions = Directory.EnumerateFiles(root, ".framepress-transaction-*.json", new EnumerationOptions { RecurseSubdirectories = recursive, IgnoreInaccessible = true, AttributesToSkip = FileAttributes.ReparsePoint }).Any();
            if (transactions) AddLog(L["RecoveryFound"]);
        }
        catch (OperationCanceledException) { Status = L["Cancelled"]; }
        catch (Exception e) { Status = L["Error"]; AddLog(e.Message); MessageBox.Show(e.Message, "FramePress"); }
        finally { Busy = false; _cancel.Dispose(); _cancel = null; }
    }
    public async Task RunAsync(bool sample, QueueItem? selected)
    {
        if (Busy) return;
        var jobs = Files.Where(f => f.Selected && !f.Completed && (f.Media.IsImage ? Images : Profiles.Single(p => p.Tier == f.Tier).Enabled)).ToList();
        if (sample)
        {
            var candidate = selected is { Media.IsImage: false } ? selected : jobs.FirstOrDefault(f => !f.Media.IsImage);
            jobs = candidate is null ? [] : [candidate];
        }
        if (jobs.Count == 0) { Status = L["NoFiles"]; return; }
        var snapshots = Profiles.ToDictionary(p => p.Tier, p => p.Snapshot());
        try { foreach (var p in snapshots.Values) p.Validate(); }
        catch (Exception e) { MessageBox.Show(e.Message, "FramePress"); return; }
        if (ImageQuality is < 50 or > 100) { MessageBox.Show("Image quality: 50–100", "FramePress"); return; }
        if (Mode == 1 && !sample && MessageBox.Show(L["ReplaceWarning"], L["Confirm"], MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var options = new JobOptions(Mode == 1, string.IsNullOrWhiteSpace(Destination) ? null : Destination, Source, ImageQuality, sample, AllowHighBitDepth, Acceleration, FullVerification);
        Busy = true; _cancel = new(); Overall = 0; Estimate = ""; Save();
        try
        {
            await _engine.VerifyToolsAsync(_cancel.Token);
            var index = 0;
            foreach (var item in jobs)
            {
                if (_cancel.IsCancellationRequested) break;
                Current = item.Name; item.State = L["Encoding"]; item.Detail = "";
                var slot = index;
                var updates = new Progress<JobProgress>(p =>
                {
                    item.Progress = p.Fraction * 100; item.State = L[p.Stage];
                    if (!string.IsNullOrWhiteSpace(p.Encoder)) EncoderSummary = p.Encoder;
                    Overall = (slot + p.Fraction) / jobs.Count * 100;
                });
                try
                {
                    var profile = item.Media.IsImage ? new CompressionProfile() : snapshots[item.Tier];
                    var result = await Task.Run(() => _engine.ProcessAsync(item.Media, profile, options, updates, _cancel.Token));
                    Results.Add(result); item.State = L[result.State]; item.Detail = result.Detail; item.Output = result.Output; item.Progress = 100; item.RecordOutput(result);
                    item.Completed = !sample && result.State is "Done" or "LargerSkipped";
                    if (result.HourMinutes is { } minutes)
                    {
                        Estimate = string.Format(L["SampleEstimate"], minutes.ToString("0.0"), Display.Bytes(result.EstimatedFullBytes ?? 0), (minutes * item.Media.Duration / 3600).ToString("0.0"), item.Name);
                        if (result.SampleSsim is { } ssim)
                            Estimate += "\n" + string.Format(L["QualityMeasured"], (ssim * 100).ToString("0.00"), ((1 - ssim) * 100).ToString("0.00"));
                        else Estimate += "\n" + L["QualityUnavailable"];
                        if (result.EstimatedFullBytes >= item.Media.Bytes) Estimate += "\n" + L["SampleLarger"];
                        Profiles.Single(p => p.Tier == item.Tier).SampleSummary = Estimate;
                    }
                    AddLog(item.Name + " · " + result.State + " · " + result.Detail);
                }
                catch (OperationCanceledException) { item.State = L["Cancelled"]; item.Progress = 0; break; }
                catch (Exception e)
                {
                    item.State = L["Error"]; item.Detail = e.Message;
                    Results.Add(new(item.Media.Path, "", "", "Error", item.Media.Bytes, item.Media.Bytes, 0, e.Message)); AddLog(item.Name + ": " + e.Message);
                }
                index++; Overall = index / (double)jobs.Count * 100;
            }
            Status = _cancel.IsCancellationRequested ? L["Cancelled"] : L["Complete"];
        }
        catch (OperationCanceledException) { Status = L["Cancelled"]; }
        catch (Exception e) { Status = L["Error"]; AddLog(e.Message); MessageBox.Show(e.Message, "FramePress"); }
        finally { Busy = false; _cancel.Dispose(); _cancel = null; }
    }
}
