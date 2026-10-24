using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Animation;
using Microsoft.Win32;
using FramePress.Core;

namespace FramePress.App;
public partial class MainWindow : Window
{
    private MainViewModel VM { get; } = new();
    internal MainViewModel ViewModel => VM;
    internal void SetThemeForSmoke(bool dark) { VM.Dark = dark; ApplyTheme(false); }
    [DllImport("dwmapi.dll", PreserveSig = true)]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
    public MainWindow()
    {
        InitializeComponent(); DataContext = VM;
        Width = Math.Min(1360, SystemParameters.WorkArea.Width - 24);
        Height = Math.Min(900, SystemParameters.WorkArea.Height - 24);
        MinHeight = Math.Min(700, Height);
        MinWidth = Math.Min(1080, Width);
        SourceInitialized += (_, _) => ApplyTheme(false);
        Loaded += async (_, _) =>
        {
            ApplyTheme(false); UpdateAfterColumns();
            if (!Environment.GetCommandLineArgs().Contains("--smoke-test")) await VM.LoadHardwareAsync();
        };
        VM.PropertyChanged += (_, change) => { if (change.PropertyName == nameof(VM.ShowAfterSize)) UpdateAfterColumns(); };
    }
    private void UpdateAfterColumns()
    {
        var visibility = VM.ShowAfterSize ? Visibility.Visible : Visibility.Collapsed;
        AfterSizeColumn.Visibility = visibility; ReductionColumn.Visibility = visibility;
    }
    private void ChooseSource(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = VM.L["ChooseFolder"] };
        if (dialog.ShowDialog(this) == true) { VM.Clear(); VM.Source = dialog.FolderName; }
    }
    private void ChooseDestination(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog { Title = VM.L["Destination"] };
        if (dialog.ShowDialog(this) == true) VM.Destination = dialog.FolderName;
    }
    private async void Scan(object sender, RoutedEventArgs e) => await VM.ScanAsync();
    private async void Start(object sender, RoutedEventArgs e) => await VM.RunFolderAsync();
    private async void ManualStart(object sender, RoutedEventArgs e) => await VM.RunAsync(false, QueueGrid.SelectedItem as QueueItem);
    private async void Sample(object sender, RoutedEventArgs e)
    {
        if (VM.Busy) return;
        var selected = QueueGrid.SelectedItem as QueueItem;
        if (selected is null || selected.Media.IsImage)
        {
            var dialog = new OpenFileDialog { Title = VM.L["Sample"], Filter = "Video|*.mp4;*.mov;*.mkv;*.avi;*.mxf;*.mts;*.m2ts;*.m4v;*.webm;*.wmv;*.mpg;*.mpeg" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var engine = new MediaEngine();
                selected = new QueueItem(await engine.ProbeAsync(dialog.FileName, CancellationToken.None));
                VM.Files.Add(selected);
            }
            catch (Exception error) { MessageBox.Show(error.Message, "FramePress"); return; }
        }
        await VM.RunAsync(true, selected);
    }
    private void Cancel(object sender, RoutedEventArgs e) => VM.Cancel();
    private void Clear(object sender, RoutedEventArgs e) => VM.Clear();
    private void SelectAll(object sender, RoutedEventArgs e)
    {
        var select = VM.Files.Any(f => !f.Selected);
        foreach (var item in VM.Files) item.Selected = select;
    }
    private void ExportReport(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Title = VM.L["Report"], FileName = "FramePress-report.csv", Filter = "CSV (*.csv)|*.csv" };
        if (dialog.ShowDialog(this) != true) return;
        try { MediaEngine.ExportCsv(VM.Results, dialog.FileName); }
        catch (Exception error) { MessageBox.Show(error.Message, "FramePress"); }
    }
    private void ToggleTheme(object sender, RoutedEventArgs e) { VM.Dark = !VM.Dark; ApplyTheme(true); VM.Save(); }
    private void ApplyTheme(bool animated)
    {
        var dark = VM.Dark;
        var colors = dark
            ? new[] { "#1B1B1B", "#242424", "#2D2D2D", "#F5F5F7", "#A6A6B0", "#3A3A3E", "#9C8CFF", "#302B46", "#62D7B4" }
            : new[] { "#F5F5FA", "#FFFFFF", "#ECECF4", "#222335", "#686A7C", "#DDDDE7", "#6850D8", "#EDE8FF", "#178563" };
        var names = new[] { "BackgroundBrush", "SurfaceBrush", "RaisedBrush", "TextBrush", "MutedBrush", "BorderBrush", "AccentBrush", "AccentSoftBrush", "SuccessBrush" };
        for (var i = 0; i < names.Length; i++) Application.Current.Resources[names[i]] = new SolidColorBrush((Color)ColorConverter.ConvertFromString(colors[i]));
        var handle = new WindowInteropHelper(this).Handle;
        if (handle != IntPtr.Zero && OperatingSystem.IsWindows())
        {
            var enabled = dark ? 1 : 0;
            _ = DwmSetWindowAttribute(handle, 20, ref enabled, sizeof(int));
        }
        var duration = TimeSpan.FromMilliseconds(animated ? 280 : 0);
        SunIcon.BeginAnimation(OpacityProperty, new DoubleAnimation(dark ? 0 : 1, duration));
        MoonIcon.BeginAnimation(OpacityProperty, new DoubleAnimation(dark ? 1 : 0, duration));
        ThemeRotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(dark ? -25 : 0, duration));
    }
    private void OpenSelected(object sender, MouseButtonEventArgs e)
    {
        if (QueueGrid.SelectedItem is not QueueItem item) return;
        var path = string.IsNullOrWhiteSpace(item.Output) ? item.Media.Path : item.Output;
        if (!File.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", "/select,\"" + path + "\"") { UseShellExecute = true }); }
        catch (Exception error) { MessageBox.Show(error.Message, "FramePress"); }
    }
    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = !VM.Busy && e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None; e.Handled = true;
    }
    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (VM.Busy || e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        var directory = paths.FirstOrDefault(Directory.Exists);
        if (directory is null) return;
        VM.Clear(); VM.Source = directory; await VM.RunFolderAsync();
    }
    private void OnClosing(object? sender, CancelEventArgs e)
    {
        if (VM.Busy) { e.Cancel = true; VM.Cancel(); return; }
        VM.Save();
    }
}
