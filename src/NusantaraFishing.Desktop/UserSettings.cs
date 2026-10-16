using System.Text.Json;
using Avalonia.Input;

namespace NusantaraFishing.Desktop;

public sealed class UserSettings
{
    public string Language { get; set; } = "id";
    public double TextScale { get; set; } = 1;
    public bool ReducedMotion { get; set; }
    public bool Muted { get; set; }
    public float Volume { get; set; } = .35f;
    public float MusicVolume { get; set; } = .25f;
    public double RenderScale { get; set; } = .8;
    public int FramesPerSecond { get; set; } = 30;
    public bool Shadows { get; set; }=true;
    public int MsaaSamples { get; set; }=4;
    public float WaterAbsorption { get; set; } = .075f;
    public float WaveStrength { get; set; } = .035f;
    public bool CrashReports { get; set; }
    public Dictionary<string, string> Keys { get; set; } = new()
    { ["forward"] = "W", ["left"] = "A", ["back"] = "S", ["right"] = "D", ["cast"] = "Space", ["board"] = "E", ["ability"] = "F", ["jump"] = "J" };
    public Key KeyFor(string action) => Enum.Parse<Key>(Keys[action]);
    public static UserSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        var settings = JsonSerializer.Deserialize<UserSettings>(File.ReadAllText(path)) ?? throw new InvalidDataException("Empty settings.");
        settings.Validate(); return settings;
    }
    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
    private void Validate()
    {
        var actions = new[] { "forward", "left", "back", "right", "cast", "board", "ability", "jump" };
        if (Language is not ("id" or "en") || !double.IsFinite(TextScale) || TextScale is < .85 or > 1.35 ||
            !double.IsFinite(RenderScale) || RenderScale is < .5 or > 1 || FramesPerSecond is < 15 or > 120 || MsaaSamples is not (1 or 2 or 4 or 8) ||
            !float.IsFinite(Volume) || Volume is < 0 or > 1 || !float.IsFinite(MusicVolume) || MusicVolume is < 0 or > 1 ||
            !float.IsFinite(WaterAbsorption) || WaterAbsorption is < .025f or > .45f || !float.IsFinite(WaveStrength) || WaveStrength is < .02f or > .2f ||
            Keys is null || actions.Any(x => !Keys.TryGetValue(x, out var key) || !Enum.TryParse<Key>(key, out var parsed) || parsed == Key.None || !Enum.IsDefined(parsed)) ||
            actions.Select(x => Keys[x]).Distinct(StringComparer.OrdinalIgnoreCase).Count() != actions.Length)
            throw new InvalidDataException("Invalid settings.");
    }
}
