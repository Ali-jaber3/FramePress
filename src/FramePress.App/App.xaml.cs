using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace FramePress.App;
public partial class App : Application
{
    private static IEnumerable<T> Descendants<T>(DependencyObject parent) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T value) yield return value;
            foreach (var nested in Descendants<T>(child)) yield return nested;
        }
    }
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

if (!e.Args.Contains("--smoke-test"))
{
    var mainWindow = new MainWindow();
    MainWindow = mainWindow;
    mainWindow.Show();
    return;
}
        Directory.CreateDirectory("artifacts");
        var logPath = Path.Combine("artifacts", "wpf-bindings.log");
        var listener = new TextWriterTraceListener(logPath);
        PresentationTraceSources.DataBindingSource.Listeners.Add(listener);
        PresentationTraceSources.DataBindingSource.Switch.Level = SourceLevels.Warning;
        try
        {
            var window = new MainWindow(); var ran = false;
            window.ContentRendered += async (_, _) =>
            {
                if (ran) return; ran = true;
                try
                {
                    foreach (var language in new[] { "ar", "en" })
                    {
                        window.ViewModel.Language = Localizer.Languages.Single(l => l.Code == language);
                        foreach (var dark in new[] { true, false })
                        {
                            window.SetThemeForSmoke(dark); window.UpdateLayout();
                            await window.Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                            var expected = dark ? "#FF1B1B1B" : "#FFF5F5FA";
                            if (window.Background is not SolidColorBrush background || background.Color.ToString() != expected)
                                throw new InvalidOperationException("Window background does not match selected theme.");
                            if (window.FlowDirection != (language == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight))
                                throw new InvalidOperationException("Language direction is incorrect.");
                            if (language == "ar" && window.ViewModel.L["Tagline"] != "فيديوهاتك، بمساحة أقل.")
                                throw new InvalidOperationException("Arabic resource encoding is corrupt.");
                            if (!Descendants<TextBlock>(window.LanguagePicker).Any(t => t.Text == window.ViewModel.Language.Name))
                                throw new InvalidOperationException("Language selector does not display the selected language name.");
                            window.ViewModel.ShowAfterSize = false; window.UpdateLayout();
                            if (window.AfterSizeColumn.Visibility != Visibility.Collapsed || window.ReductionColumn.Visibility != Visibility.Collapsed)
                                throw new InvalidOperationException("Output-size columns did not hide.");
                            window.ViewModel.ShowAfterSize = true; window.UpdateLayout();
                            if (window.AfterSizeColumn.Visibility != Visibility.Visible || window.ReductionColumn.Visibility != Visibility.Visible)
                                throw new InvalidOperationException("Output-size columns did not become visible.");
                            var bitmap = new RenderTargetBitmap((int)window.ActualWidth, (int)window.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                            bitmap.Render(window);
                            var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                            var name = $"FramePress-ui-{language}-{(dark ? "dark" : "light")}.png";
                            using var output = File.Create(Path.Combine("artifacts", name)); encoder.Save(output);
                        }
                    }
                    listener.Flush();
PresentationTraceSources.DataBindingSource.Listeners.Remove(listener);
listener.Close();

var errors = File.ReadAllText(logPath)
    .Contains("System.Windows.Data Error", StringComparison.Ordinal);
                    window.Close(); Shutdown(errors ? 1 : 0);
                }
                catch (Exception error) { File.WriteAllText(Path.Combine("artifacts", "ui-smoke-error.txt"), error.ToString()); Shutdown(1); }
            };
            window.Show();
        }
        catch (Exception error) { File.WriteAllText(Path.Combine("artifacts", "ui-smoke-error.txt"), error.ToString()); Shutdown(1); }
    }
}
