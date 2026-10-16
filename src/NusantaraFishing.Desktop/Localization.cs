using System.Text.Json;
using Avalonia.Controls;

namespace NusantaraFishing.Desktop;

public static class Localization
{
    private sealed record TextEntry(WeakReference<TextBlock> Target, string Original, double Size);
    private sealed record ButtonEntry(WeakReference<Button> Target, string Original);
    private static readonly List<TextEntry> texts = [];
    private static readonly List<ButtonEntry> buttons = [];
    private static readonly Dictionary<string, string> English = Load();
    private static readonly KeyValuePair<string, string>[] Fragments = English.OrderByDescending(x => x.Key.Length).ToArray();
    public static string Language { get; private set; } = "id";
    public static double TextScale { get; private set; } = 1;

    private static Dictionary<string, string> Load()
    {
        using var stream = typeof(Localization).Assembly.GetManifestResourceStream("NusantaraFishing.Desktop.Locales.en.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }
    public static string T(string value)
    {
        if (Language == "id" || value.Length == 0) return value;
        if (English.TryGetValue(value, out var exact)) return exact;
        // Dynamic labels combine named content, numbers, and translated sentence fragments.
        var result = value;
        foreach (var pair in Fragments)
            if (pair.Key.Length >= 4 && result.Contains(pair.Key, StringComparison.Ordinal)) result = result.Replace(pair.Key, pair.Value, StringComparison.Ordinal);
        return result;
    }
    public static TextBlock Register(TextBlock block, string original, double size)
    {
        if (texts.Count > 300) texts.RemoveAll(x => !x.Target.TryGetTarget(out _));
        texts.Add(new(new(block), original, size)); block.Text = T(original); block.FontSize = size * TextScale; return block;
    }
    public static Button Register(Button button, string original)
    {
        if (buttons.Count > 200) buttons.RemoveAll(x => !x.Target.TryGetTarget(out _));
        buttons.Add(new(new(button), original)); button.Content = T(original); button.FontSize = 14 * TextScale; return button;
    }
    public static void Configure(string language, double textScale)
    {
        Language = language; TextScale = textScale;
        texts.RemoveAll(x => !x.Target.TryGetTarget(out _));
        buttons.RemoveAll(x => !x.Target.TryGetTarget(out _));
        foreach (var item in texts)
            if (item.Target.TryGetTarget(out var block)) { if (item.Original.Length > 0) block.Text = T(item.Original); block.FontSize = item.Size * TextScale; }
        foreach (var item in buttons)
            if (item.Target.TryGetTarget(out var button)) { button.Content = T(item.Original); button.FontSize = 14 * TextScale; }
    }
}
