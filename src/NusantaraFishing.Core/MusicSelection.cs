namespace NusantaraFishing.Core;

/// <summary>Pure soundtrack policy, shared by the audio mixer and regression tests.</summary>
public static class MusicSelection
{
    public static readonly string[] Tracks = [.. Catalog.Regions.Select(x => "region-" + x.Id), "boss-theme", "dj-theme"];

    public static string For(GameSession game)
    {
        if (game.Phase == FishingPhase.Fighting)
        {
            if (game.Hooked?.Kind == CatchKind.Boss) return "boss-theme";
            if (game.Hooked?.Id is "dj" or "disco") return "dj-theme";
        }
        return "region-" + game.Region.Id;
    }
}
