namespace NusantaraFishing.Core;

public sealed class PlayerState
{
    public int Version { get; set; } = 2;
    public string CharacterId { get; set; } = "ujang";
    public string RegionId { get; set; } = "selatan";
    public int Coins { get; set; } = 300;
    public int Experience { get; set; }
    public int RodLevel { get; set; } = 1;
    public int BoatIndex { get; set; }
    public int TotalCaught { get; set; }
    public double WorldHours { get; set; } = 6.5;
    public double ExhibitionSeconds { get; set; }
    public string BaitId { get; set; } = "worm";
    public float PositionX { get; set; }
    public float PositionZ { get; set; } = 5;
    public float BoatX { get; set; }
    public float BoatZ { get; set; } = 5;
    /// <summary>Bow yaw in radians; the bow points along (-sin, -cos).</summary>
    public float BoatHeading { get; set; }
    public bool OnFoot { get; set; }
    public CatchingMethod CatchingMethod { get; set; }
    public bool InWater => CatchingMethod is CatchingMethod.Snorkel or CatchingMethod.Spear;
    public bool Climbing { get; set; }
    public double AbilityCooldown { get; set; }
    public Dictionary<string, int> RegionCatches { get; set; } = [];
    public Dictionary<string, int> ExhibitSlots { get; set; } = [];
    public int AquariumLevel { get; set; } = 1;
    public int MuseumLevel { get; set; } = 1;
    public int VisitorsWelcomed { get; set; }
    public List<CatchRecord> Inventory { get; set; } = [];
    public HashSet<string> Discovered { get; set; } = [];
    public HashSet<string> Exhibits { get; set; } = [];
    public HashSet<string> DefeatedBosses { get; set; } = [];
    public HashSet<string> ClaimedQuests { get; set; } = [];
    public List<TournamentTrophy> Trophies { get; set; } = [];
    public int Level => Math.Min(10, 1 + Experience / 150);
    public PlayerState Copy()
    {
        var copy=(PlayerState)MemberwiseClone();
        copy.Inventory=[..Inventory]; copy.Discovered=[..Discovered]; copy.Exhibits=[..Exhibits];
        copy.DefeatedBosses=[..DefeatedBosses]; copy.ClaimedQuests=[..ClaimedQuests];
        copy.Trophies=[..Trophies];
        copy.RegionCatches=new(RegionCatches); copy.ExhibitSlots=new(ExhibitSlots);
        return copy;
    }
}

public sealed record TournamentTrophy(string Period, int Rank, DateTimeOffset AwardedAt);

public sealed record Quest(string Id, string Title, string Description, int Target, int Reward);
public static class Quests
{
    public static readonly Quest[] All =
    [
        new("first", "Lemparan pertama", "Tangkap 1 makhluk atau benda.", 1, 100),
        new("angler", "Cerita dari dermaga", "Kumpulkan 10 tangkapan.", 10, 400),
        new("collector", "Peneliti nusantara", "Temukan 8 spesies berbeda.", 8, 700),
        new("museum", "Rumah untuk cerita", "Sumbangkan 5 spesies ke pameran.", 5, 900),
        new("boss", "Penjaga laut", "Kalahkan 1 boss regional.", 1, 1500),
        new("raja", "Raja Nusantara", "Temukan Ikan Raja Nusantara.", 1, 10000),
        new("ancient-map", "Pesan Kura-Kura Kuno", "Temukan Kura-Kura Kuno, lalu Peta Rahasia Nusantara.", 2, 1800),
        new("dugong-guardian", "Sahabat Dugong", "Catat dan lepaskan Dugong Nusantara.", 1, 2200),
        new("lost-delivery", "Barang yang pulang", "Temukan Motor Hilang dan ATM Tenggelam.", 2, 1200),
        new("final-whale", "Riak terakhir", "Kalahkan Paus Kubangan setelah tiga penjaga endgame.", 1, 5000),
    .. Catalog.Regions.Select(r => new Quest("region-"+r.Id,"Penjaga "+r.Name,
        "Daratan ini punya cerita. Dapatkan 5 tangkapan di "+r.Name+".",5,250+r.Level*100))
    ];
    public static int Progress(Quest quest, PlayerState state) => quest.Id switch
    {
        "collector" => state.Discovered.Count,
        "museum" => state.Exhibits.Count,
        "boss" => state.DefeatedBosses.Count,
        "raja" => state.Discovered.Contains("raja") ? 1 : 0,
        "ancient-map" => new[] { "kura", "peta" }.Count(state.Discovered.Contains),
        "dugong-guardian" => state.Discovered.Contains("dugong") ? 1 : 0,
        "lost-delivery" => new[] { "motor", "atm" }.Count(state.Discovered.Contains),
        "final-whale" => state.Discovered.Contains("paus-kubangan") ? 1 : 0,
        var id when id.StartsWith("region-", StringComparison.Ordinal) => state.RegionCatches.GetValueOrDefault(id[7..]),
        _ => state.TotalCaught
    };
    public static bool Visible(Quest quest, PlayerState state) => quest.Id switch
    {
        "ancient-map" => state.Discovered.Contains("kura"),
        "lost-delivery" => state.Discovered.Overlaps(["motor", "atm"]),
        "final-whale" => new[] { "gunung", "kristal", "dimensi" }.All(state.DefeatedBosses.Contains),
        _ => true
    };
}
