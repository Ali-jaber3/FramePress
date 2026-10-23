using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using FramePress.Core;

namespace FramePress.App;
public sealed record LanguageOption(string Code, string Name);
public sealed class Localizer : Observable
{
    private Dictionary<string, string> _strings = [];
    public static LanguageOption[] Languages { get; } = [new("ar", "العربية"), new("en", "English"), new("fr", "Français"), new("es", "Español"), new("de", "Deutsch"), new("pt", "Português"), new("it", "Italiano"), new("tr", "Türkçe"), new("hi", "हिन्दी"), new("zh", "中文")];
    public string Code { get; private set; } = "ar";
    public FlowDirection Direction => Code == "ar" ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    public string this[string key] => _strings.GetValueOrDefault(key, key);
    public Localizer(string code) => SetLanguage(code);
    public void SetLanguage(string code)
    {
        if (!Languages.Any(l => l.Code == code)) code = "en";
        Code = code;
        var assembly = Assembly.GetExecutingAssembly();
        var resource = assembly.GetManifestResourceNames().Single(n => n.EndsWith(".Languages." + code + ".json", StringComparison.Ordinal));
        using var stream = assembly.GetManifestResourceStream(resource)!;
        _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? [];
        Raise("Item[]"); Raise(nameof(Direction)); Raise(nameof(Code));
    }
}
