using System.IO;
using System.Text.Json;
using FramePress.Core;

namespace FramePress.App;
public sealed class Preferences
{
    public string Language { get; set; } = "ar";
    public bool Dark { get; set; } = true;
    public bool Recursive { get; set; } = true;
    public bool Images { get; set; }
    public int ImageQuality { get; set; } = 85;
    public bool AllowHighBitDepth { get; set; }
    public string Acceleration { get; set; } = "Auto";
    public bool FullVerification { get; set; }
    public bool ShowAfterSize { get; set; } = true;
    public bool SpeedUpgradeApplied { get; set; }
    public List<CompressionProfile> Profiles { get; set; } = [];
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FramePress");
    public static Preferences Load()
    {
        try
        {
            var value = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(Path.Combine(Folder, "settings.json"))) ?? new();
            value.ImageQuality = Math.Clamp(value.ImageQuality, 50, 100);
            value.Profiles = (value.Profiles ?? []).Where(p => CompressionProfile.Tiers.Contains(p.Tier)).GroupBy(p => p.Tier).Select(g => g.First()).ToList();
            foreach (var p in value.Profiles) p.Validate();
            if (!EncoderPolicy.Modes.Contains(value.Acceleration)) value.Acceleration = "Auto";
            if (!value.SpeedUpgradeApplied)
            {
                foreach (var profile in value.Profiles.Where(p => p.Preset == "medium")) profile.Preset = "veryfast";
                value.SpeedUpgradeApplied = true;
            }
            return value;
        }
        catch (Exception e) when (e is IOException or JsonException or ArgumentException or UnauthorizedAccessException) { return new(); }
    }
    public void Save()
    {
        Directory.CreateDirectory(Folder); var target = Path.Combine(Folder, "settings.json"); var temp = target + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, target, true);
    }
}
