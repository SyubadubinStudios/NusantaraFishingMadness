namespace NusantaraFishing.Core;

public enum CatchingMethod { Rod, Snorkel, Spear, Hand }

public sealed partial class GameSession
{
    private double missedReflexSeconds;

    private bool CanReachWater()
    {
        var position = Traversal.Position(Player);
        for (var i = 0; i < 16; i++)
        {
            var angle = i * MathF.Tau / 16;
            if (World.CanSail(position + new System.Numerics.Vector2(MathF.Cos(angle), MathF.Sin(angle)) * 2.5f, .1f)) return true;
        }
        return false;
    }

    private bool CanEncounter(Species species)
    {
        if (species.Kind == CatchKind.Boss && (Player.RodLevel < 3 || Player.TotalCaught < 10)) return false;
        if (species.Id == "paus-kubangan" && !new[] { "gunung", "kristal", "dimensi" }.All(Player.DefeatedBosses.Contains)) return false;
        return Player.CatchingMethod switch
        {
            CatchingMethod.Snorkel => species.Kind == CatchKind.Conservation,
            CatchingMethod.Spear => species.Kind is CatchKind.Fish or CatchKind.Cephalopod or CatchKind.Crustacean,
            CatchingMethod.Hand => species.Kind is CatchKind.Fish or CatchKind.Crustacean or CatchKind.Amphibian,
            _ => true
        };
    }

    public bool SelectCatchingMethod(string method)
    {
        if (Busy || !Enum.TryParse<CatchingMethod>(method, true, out var selected) || !Enum.IsDefined(selected)) return false;
        if (selected is CatchingMethod.Snorkel or CatchingMethod.Spear && (Region.Id != "ampat" || Player.OnFoot))
        { Message = "Snorkeling dan tombak tersedia dari perahu di Raja Ampat."; return false; }
        if (selected == CatchingMethod.Hand && (Character.Id != "ojan" || !Player.OnFoot || Player.Climbing))
        { Message = "Ojan menangkap dengan tangan saat berdiri di tepian."; return false; }
        if (selected == CatchingMethod.Hand && !CanReachWater())
        { Message = "Dekati tepi air agar tangan Ojan dapat menjangkau tangkapan."; return false; }
        if (Player.InWater && selected == CatchingMethod.Rod)
        {
            if (System.Numerics.Vector2.Distance(Traversal.Position(Player), new(Player.BoatX, Player.BoatZ)) > 4)
            { Message = "Dekati perahu untuk kembali menggunakan joran."; return false; }
            Player.CatchingMethod = selected;
            Traversal.SetPosition(Player, new(Player.BoatX, Player.BoatZ));
        }
        else
        {
            var enteringWater = !Player.InWater && selected is CatchingMethod.Snorkel or CatchingMethod.Spear;
            if (enteringWater)
            {
                var point = Traversal.Position(Player) + new System.Numerics.Vector2(1.5f, 0);
                if (!World.CanSail(point, .3f))
                { Message = "Geser perahu ke air terbuka sebelum menyelam."; return false; }
                Player.CatchingMethod = selected;
                Traversal.SetPosition(Player, point);
            }
            else Player.CatchingMethod = selected;
        }
        Dive = null;
        Message = selected switch
        {
            CatchingMethod.Snorkel => "Snorkeling: cari satwa, berenang mendekat, lalu tahan aksi untuk memusatkan bidikan dan dokumentasi.",
            CatchingMethod.Spear => "Tombak: cari target, berenang mendekat, tahan aksi untuk membidik, lalu lepaskan untuk melempar.",
            CatchingMethod.Hand => "Tangkapan tangan: Ojan siap menangkap ikan, kepiting, dan katak di tepian.",
            _ => "Joran siap digunakan."
        };
        return true;
    }

    public bool ClimbTree()
    {
        if (Busy || !Player.OnFoot || Character.Id != "ojan") return false;
        if (Player.Climbing) { Player.Climbing = false; Message = "Ojan turun dari pohon."; return true; }
        var point = Traversal.Position(Player);
        var trees = World.TreePositions().Where(x => System.Numerics.Vector2.Distance(x, point) <= 2.5f).ToArray();
        if (trees.Length == 0) { Message = "Dekati pohon di tepian, lalu pilih Panjat pohon."; return false; }
        var tree = trees.OrderBy(x => System.Numerics.Vector2.DistanceSquared(x, point)).First();
        Traversal.SetPosition(Player, tree);
        safeFootPosition = tree;
        Player.Climbing = true;
        Player.CatchingMethod = CatchingMethod.Rod;
        Message = "Ojan memanjat. Tekan Lompat untuk melompat dari pohon atau Panjat pohon untuk turun.";
        return true;
    }

    public bool Barter(string offer)
    {
        if (Busy || Character.Id != "rina") return false;
        var recipe = offer switch
        {
            "keris" => (Input: "sandal", Count: 3, Output: "keris"),
            "peta" => (Input: "kulkas", Count: 2, Output: "peta"),
            _ => default
        };
        if (recipe.Input is null) return false;
        var items = Player.Inventory.Where(x => x.SpeciesId == recipe.Input).Take(recipe.Count).ToArray();
        if (items.Length != recipe.Count)
        { Message = $"Barter memerlukan {recipe.Count} × {Catalog.FindSpecies(recipe.Input).Name} di tas."; return false; }
        foreach (var item in items) Player.Inventory.Remove(item);
        var species = Catalog.FindSpecies(recipe.Output);
        Player.Inventory.Add(new(species.Id, 1, species.Value, DateTimeOffset.UtcNow, Region.Id));
        Player.Discovered.Add(species.Id);
        Message = $"Barter berhasil: {species.Name}.";
        return true;
    }

    private string ResearchReport()
    {
        var rare = AvailableSpecies().Where(x => x.Rarity >= Rarity.Rare).OrderBy(x => x.Difficulty).Take(3);
        var forecast = string.Join(", ", rare.Select(x => x.Name));
        return $"Scan: {Hooked?.Name ?? "belum ada target"}. Cuaca: {Weather}; perubahan dalam {Math.Ceiling(75 - weatherSeconds)} dtk. Kandidat langka: {forecast}.";
    }

    private double CreatureAttack(bool reeling, double direction, double seconds)
    {
        var cycle = fightSeconds % 6;
        var aligned = Math.Abs(direction - FishDirection) < .65;
        switch (Hooked!.Id)
        {
            case "gurita" when cycle < 2:
                BossCue = "KAMUFLASE BATU • Lepaskan tarikan sampai gurita bergerak.";
                return reeling ? .16 : 0;
            case "gurita" when cycle < 3:
            case "phantom" when cycle < 3:
                FishDirection = -FishDirection;
                BossCue = "TINTA • Arah berubah! Ikuti indikator.";
                return reeling && !aligned ? .15 : 0;
            case "roket" when cycle < 1.4:
                BossCue = "CUMI ROKET • Lepas saat melesat!";
                return reeling ? .25 : 0;
            case "kepiting":
                BossCue = cycle < 2 ? "DUEL CAPIT • Tangkis: lepas tarikan." : "DUEL CAPIT • Balas: tarik mengikuti arah.";
                return cycle < 2 && reeling ? .25 : cycle >= 2 && aligned ? -.08 : 0;
            case "ninja":
                if (cycle < 1.5)
                { missedReflexSeconds = 0; BossCue = "KATAK NINJA • Bersiap mengikuti arah!"; }
                else if (cycle < 3)
                {
                    BossCue = "REFLEKS • Tahan Tarik dan cocokkan arah A / D!";
                    missedReflexSeconds = reeling && aligned ? 0 : missedReflexSeconds + seconds;
                    if (missedReflexSeconds > .9)
                    { Phase = FishingPhase.Escaped; Message = "Katak Ninja menghindar. Saat REFLEKS, tarik sambil mengikuti arah."; }
                }
                return 0;
            case "belut" when cycle < 1.5:
            case "buaya" when cycle < 1.5:
                BossCue = Hooked.Id == "belut" ? "SENGATAN • Lepas tarikan!" : "TERJANGAN BUAYA • Lepas tarikan!";
                return reeling ? .23 : 0;
            default: return 0;
        }
    }
}
