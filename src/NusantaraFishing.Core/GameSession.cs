using System.Numerics;

namespace NusantaraFishing.Core;

/// <summary>UI-independent simulation. All time values are elapsed simulation seconds.</summary>
public sealed partial class GameSession
{
    private readonly Random random;
    private double phaseSeconds;
    private double fightSeconds;
    private double weatherSeconds;
    private double abilitySeconds;
    private double reefSeconds;
    private bool deferredBossRewards;
    internal bool BossRewardsDeferred => deferredBossRewards;
    private double? sharedEncounterSeconds;
    public PlayerState Player { get; private set; }
    public Character Character => Catalog.Characters.First(x => x.Id == Player.CharacterId);
    public Region Region => Catalog.Regions.First(x => x.Id == Player.RegionId);
    public WorldDefinition World => Worlds.For(Player.RegionId);
    public Bait Bait => WorldDefinition.Baits.First(x => x.Id == Player.BaitId);
    public double Depth => World.DepthAt(Player.PositionX, Player.PositionZ - 6);
    public double AbilitySeconds => abilitySeconds;
    public bool AbilityActive => abilitySeconds > 0;
    public string BossCue { get; private set; } = "";
    public float JumpHeight { get; private set; }
    private double jumpSeconds;
    private double jumpDuration = 1.1;
    private float jumpStartHeight;
    private Vector2 safeFootPosition = new(-7, 2);
    public Vector2 HookPosition => new(Player.PositionX + (float)FishDirection * 4, Player.PositionZ - 7);
    public FishingPhase Phase { get; private set; }
    public Weather Weather { get; private set; }
    public Species? Hooked { get; private set; }
    public CatchRecord? LastCatch { get; private set; }
    public double Tension { get; private set; }
    public double Progress { get; private set; }
    public double FishDirection { get; private set; }
    public string Message { get; private set; } = "Selamat datang di dermaga. Lempar kail untuk memulai.";
    public bool Busy => Phase is FishingPhase.Waiting or FishingPhase.Fighting;
    public int UpgradeCost => (int)(Player.RodLevel * 250 * (1 - Character.Discount - (Character.Id == "rina" && AbilityActive ? .2 : 0)));

    public GameSession(PlayerState? player = null, Random? random = null)
    {
        Player = player ?? new();
        this.random = random ?? new();
    }

    public bool SelectCharacter(string id)
    {
        if (Busy || !Catalog.Characters.Any(x => x.Id == id) || Player.Level < UnlockLevel(id)) return false;
        Player.CharacterId = id;
        Player.CatchingMethod = CatchingMethod.Rod;
        Player.Climbing = false;
        if (Player.OnFoot && !World.CanWalk(Traversal.Position(Player), id is "ojan" or "pendaki", id == "tono")) Player.OnFoot = false;
        if (!Player.OnFoot) Traversal.SetPosition(Player, new(Player.BoatX, Player.BoatZ));
        abilitySeconds = 0;
        Message = $"{Character.Name} siap berlayar. {Character.Bonus}.";
        return true;
    }

    public bool Travel(string id)
    {
        var region = Catalog.Regions.FirstOrDefault(x => x.Id == id);
        if (Busy || region is null) return false;
        if (region.Id == Player.RegionId) return true;
        if (Player.Level + (Character.Id == "tono" && AbilityActive ? 1 : 0) < region.Level) { Message = $"Wilayah ini terbuka di level {region.Level}."; return false; }
        var cost = Character.Id == "tono" ? 0 : region.TravelCost;
        if (Player.Coins < cost) { Message = "Koin perjalanan belum cukup. Jual tangkapan di pasar."; return false; }
        Player.Coins -= cost;
        Player.RegionId = region.Id;
        Player.OnFoot = false;
        Player.CatchingMethod = CatchingMethod.Rod;
        Player.Climbing = false;
        jumpSeconds = 0;
        JumpHeight = 0;
        Traversal.SetPosition(Player, new(0, 5));
        Phase = FishingPhase.Idle;
        Message = $"Tiba di {region.Name}. {region.Description}";
        return true;
    }

    public bool Cast(bool challengeBoss = false,bool deferBossRewards=false)
    {
        if (Busy) return false;
        if (challengeBoss && Player.InWater) { Message = "Kembali ke perahu sebelum menantang boss."; return false; }
        if (Player.CatchingMethod == CatchingMethod.Hand && !CanReachWater())
        { Message = "Dekati tepi air agar tangan Ojan dapat menjangkau tangkapan."; return false; }
        if (Player.Inventory.Count >= 200) { Message = "Tas penuh. Jual atau sumbangkan tangkapan."; return false; }
        if (challengeBoss && (Player.RodLevel < 3 || Player.TotalCaught < 10))
        { Message = "Boss membutuhkan joran level 3 dan 10 tangkapan."; return false; }
        var baitCost = challengeBoss || Player.CatchingMethod == CatchingMethod.Rod ? Bait.Cost : 0;
        if (Player.Coins < baitCost) { Message = "Koin umpan belum cukup. Pilih cacing gratis."; return false; }
        Player.Coins -= baitCost;
        deferredBossRewards=challengeBoss && deferBossRewards;
        if (challengeBoss) Player.CatchingMethod = CatchingMethod.Rod;
        Hooked = challengeBoss ? Catalog.BossSpecies(Region) : RollSpecies();
        Phase = FishingPhase.Waiting;
        phaseSeconds = Player.CatchingMethod == CatchingMethod.Rod ? 1.5 + random.NextDouble() * 2 : .6;
        Progress = 0;
        Tension = .25;
        fightSeconds = 0;
        reefSeconds = 0;
        missedReflexSeconds = 0;
        BossCue = "";
        Message = "Kail terlempar. Tunggu sampai ada yang menyambar…";
        return true;
    }

    public Species[] AvailableSpecies() => Catalog.Species.Where(s =>
        (s.Regions.Contains("*") || s.Regions.Contains(Region.Id)) &&
        (!s.Weather.HasValue || s.Weather == Weather) && CanEncounter(s)).ToArray();

    private Species RollSpecies()
    {
        var pool = AvailableSpecies();
        double Weight(Species s)
        {
            var value = s.Rarity switch
            {
                Rarity.Common => 40,
                Rarity.Uncommon => 25,
                Rarity.Rare => 12 * (1 + Character.Luck + (Weather == Weather.Rain ? .3 : 0)),
                Rarity.Epic => 5 * (1 + Character.Luck),
                _ => 1.5 * (1 + Character.Luck + (Player.WorldHours >= 18 ? .2 : 0))
            };
            if (Bait.PreferredKind == s.Kind || (Bait.Id == "relic" && s.Kind == CatchKind.Junk) || (Bait.Id == "shrimp" && s.Kind == CatchKind.Crustacean)) value *= 2.5;
            if (s.Rarity >= Rarity.Rare) value *= 1 + Depth * .08 + Bait.DepthBonus + (AbilityActive && Character.Id == "kucing" ? 1 : 0) + (Player.Discovered.Contains("kodok") ? .15 : 0);
            if (World.Biome is Biome.Quarry or Biome.ThreeLakes && s.Kind == CatchKind.Mutant) value *= 2;
            if (World.Biome is Biome.City or Biome.Pothole && s.Kind == CatchKind.Junk) value *= 1.6;
            if (World.Underground && s.Kind == CatchKind.Shadow) value *= 1.6;
            return value;
        }
        var roll = random.NextDouble() * pool.Sum(Weight);
        foreach (var species in pool) { roll -= Weight(species); if (roll <= 0) return species; }
        return pool[^1];
    }

    public void Tick(double seconds, bool reeling = false, double direction = 0)
    {
        if (!double.IsFinite(seconds) || seconds <= 0) return;
        // Bound integration steps so frame stalls do not instantly break the line.
        seconds = Math.Min(seconds, .1);
        direction = double.IsFinite(direction) ? Math.Clamp(direction, -1, 1) : 0;
        abilitySeconds = Math.Max(0, abilitySeconds - seconds);
        Player.AbilityCooldown = Math.Max(0, Player.AbilityCooldown - seconds);
        jumpSeconds = Math.Max(0, jumpSeconds - seconds);
        JumpHeight = jumpSeconds > 0 ? jumpStartHeight * (float)(jumpSeconds / jumpDuration) + (float)Math.Sin(jumpSeconds / jumpDuration * Math.PI) * 1.2f : 0;
        if (Player.OnFoot && jumpSeconds == 0 && !World.CanWalk(Traversal.Position(Player), Character.Id is "ojan" or "pendaki", Character.Id == "tono"))
        { Traversal.SetPosition(Player, safeFootPosition); Message = "Kembali ke pijakan aman."; }
        Player.WorldHours = (Player.WorldHours + seconds / 30) % 24;
        weatherSeconds += seconds;
        if (weatherSeconds >= 75) { weatherSeconds = 0; Weather = (Weather)random.Next(5); }
        Player.ExhibitionSeconds += seconds;
        if (Player.ExhibitionSeconds >= 60)
        {
            Player.ExhibitionSeconds -= 60;
            var visitors = Player.ExhibitSlots.Count > 0 ? Math.Max(1, (Player.AquariumLevel + Player.MuseumLevel) / 2) : 0;
            Player.VisitorsWelcomed += visitors;
            Player.Coins += Player.Exhibits.Count * 3 + visitors * 2;
        }
        if (Phase == FishingPhase.Waiting)
        {
            phaseSeconds -= seconds;
            if (phaseSeconds <= 0) { Phase = FishingPhase.Fighting; Message = "Sambaran! Tahan Tarik, lepas saat tegangan tinggi. A / D mengikuti ikan."; }
        }
        else if (Phase == FishingPhase.Fighting && Hooked is not null)
        {
            fightSeconds += seconds;
            FishDirection = Math.Sin(fightSeconds * (1.2 + Hooked.Difficulty));
            if (AbilityActive && Character.Id == "robot") direction = FishDirection;
            var aligned = Math.Abs(direction - FishDirection) < .65;
            var strength = Character.Strength + Catalog.Boats[Player.BoatIndex].Strength + (Player.RodLevel - 1) * .07 +
                (AbilityActive && Character.Id is "ujang" or "ojan" or "bakso" ? .3 : 0);
            var weatherForce = Weather is Weather.Storm or Weather.Lightning ? .06 : 0;
            var surge = BossAttack(reeling);
            surge += CreatureAttack(reeling, direction, seconds);
            if (Phase != FishingPhase.Fighting) return;
            if (World.Biome == Biome.Volcano && fightSeconds % 12 is > 7 and < 9)
            { BossCue = "GEYSER • Lepas tarikan sampai semburan reda!"; if (reeling) surge += .18; }
            var lineHits = World.LineHitsReef(Traversal.Position(Player), HookPosition);
            reefSeconds = lineHits && reeling ? reefSeconds + seconds : Math.Max(0, reefSeconds - seconds * 2);
            if (lineHits) BossCue = "TALI MENYENTUH KARANG • Ubah arah A / D, kurangi tarikan!";
            if (AbilityActive && Character.Id == "satpam") surge = 0;
            var currentForce = World.Current.Length() * .07;
            Tension = Math.Clamp(Tension + seconds * (reeling ? .15 + Hooked.Difficulty * .17 + weatherForce + surge + currentForce - strength * .12 - (aligned ? .08 : 0) : -.42), 0, 1);
            var methodBonus = Player.CatchingMethod == CatchingMethod.Spear && aligned ? .14 : 0;
            Progress = Math.Clamp(Progress + seconds * (reeling ? .14 + strength * .1 + (aligned ? .07 : 0) + methodBonus - Hooked.Difficulty * .065 : -.025), 0, 1);
            if (Tension >= 1 || fightSeconds > 65 || reefSeconds > 2.5) { Phase = FishingPhase.Escaped; Message = reefSeconds > 2.5 ? "Tali putus tergesek karang. Ubah posisi sebelum melempar lagi." : "Tangkapan lepas. Atur tegangan dan coba lagi."; }
            else if (Progress >= 1) LandCatch();
        }
    }

    private void LandCatch()
    {
        var species = Hooked!;
        var weight = Math.Round((.3 + random.NextDouble() * 4) * (species.Kind == CatchKind.Boss ? 30 : 1), 2);
        LastCatch = new(species.Id, weight, (int)(species.Value * (1 + Character.SellBonus)), DateTimeOffset.UtcNow, Region.Id);
        if(species.Kind==CatchKind.Boss && deferredBossRewards)
        {
            Phase=FishingPhase.Landed;
            Message="Serangan tim mengenai boss. Lanjutkan sampai HP bersama habis.";
            return;
        }
        // Conservation encounters are documented and released, never traded or exhibited.
        if (species.Kind != CatchKind.Conservation) Player.Inventory.Add(LastCatch);
        Player.Discovered.Add(species.Id);
        Player.TotalCaught++;
        Player.RegionCatches[Region.Id] = Player.RegionCatches.GetValueOrDefault(Region.Id) + 1;
        Player.Experience += 30 + (int)species.Rarity * 15;
        if (species.Id == "boss-" + Region.Id) Player.DefeatedBosses.Add(Region.Id);
        Phase = FishingPhase.Landed;
        Message = species.Kind == CatchKind.Conservation ? $"{species.Name} dicatat dan dilepas kembali. +60 koin konservasi." : $"{species.Name} • {weight:N2} kg • +{30 + (int)species.Rarity * 15} XP";
        if (species.Kind == CatchKind.Conservation) Player.Coins += 60;
        if (species.Id == "profesor") Message += " Hipotesis saya: umpan Anda signifikan secara statistik.";
    }
    public void FinishSharedBoss(string regionId)
    {
        if(regionId!=Region.Id) throw new InvalidOperationException("Boss region does not match player.");
        Hooked=Catalog.BossSpecies(Region); deferredBossRewards=false; LandCatch();
    }
    public SimulationSnapshot Snapshot()=>new(Player.Copy(),Phase,Weather,Hooked?.Id,LastCatch,Tension,Progress,FishDirection,BossCue,Message,abilitySeconds,JumpHeight);
    public void ApplySnapshot(SimulationSnapshot snapshot)
    {
        if(snapshot.Player is null || snapshot.Message is null || snapshot.BossCue is null ||
            (snapshot.HookedId is { } id && !Catalog.Species.Any(x=>x.Id==id) && !Catalog.Regions.Any(x=>"boss-"+x.Id==id)))
            throw new InvalidDataException("Invalid server snapshot references.");
        SaveStore.Validate(snapshot.Player);
        if(!Enum.IsDefined(snapshot.Phase) || !Enum.IsDefined(snapshot.Weather) || !double.IsFinite(snapshot.Tension) ||
            snapshot.Tension is <0 or >1 || !double.IsFinite(snapshot.Progress) || snapshot.Progress is <0 or >1 ||
            !double.IsFinite(snapshot.FishDirection) || Math.Abs(snapshot.FishDirection)>1.01 ||
            !double.IsFinite(snapshot.AbilitySeconds) || snapshot.AbilitySeconds is <0 or >12 ||
            !float.IsFinite(snapshot.JumpHeight) || snapshot.JumpHeight is <0 or >5 || snapshot.Message.Length>2048 || snapshot.BossCue.Length>512)
            throw new InvalidDataException("Invalid server simulation snapshot.");
        Player=snapshot.Player; Phase=snapshot.Phase; Weather=snapshot.Weather;
        Hooked=snapshot.HookedId is null?null:Catalog.FindSpecies(snapshot.HookedId);
        LastCatch=snapshot.LastCatch; Tension=snapshot.Tension; Progress=snapshot.Progress;
        FishDirection=snapshot.FishDirection; BossCue=snapshot.BossCue; Message=snapshot.Message;
        abilitySeconds=snapshot.AbilitySeconds; JumpHeight=snapshot.JumpHeight;
    }
    public void SynchronizeEnvironment(double hours,Weather weather)
    { Player.WorldHours=hours; Weather=weather; }
    public void SynchronizeBossClock(double? seconds)=>sharedEncounterSeconds=seconds;
    public void AbortFishing(string message)
    { Phase=FishingPhase.Escaped; deferredBossRewards=false; sharedEncounterSeconds=null; BossCue=""; Message=message; }

    public int SellAll()
    {
        if (Busy) return 0;
        var amount = Player.Inventory.Sum(x => x.Value);
        Player.Inventory.Clear();
        Player.Coins += amount;
        Message = $"Pasar dermaga: +{amount:N0} koin. Koleksi penemuan tetap tersimpan.";
        return amount;
    }

    public bool Donate(string speciesId)
    {
        if (Busy || Player.Exhibits.Contains(speciesId)) return false;
        var item = Player.Inventory.FirstOrDefault(x => x.SpeciesId == speciesId);
        if (item is null) return false;
        Player.Inventory.Remove(item);
        Player.Exhibits.Add(speciesId);
        Message = $"{Catalog.FindSpecies(speciesId).Name} masuk pameran. Pendapatan +3 koin/menit.";
        return true;
    }

    public bool UpgradeRod()
    {
        if (Busy || Player.RodLevel >= 10) return false;
        if (Player.Coins < UpgradeCost) { Message = "Koin belum cukup untuk upgrade joran."; return false; }
        Player.Coins -= UpgradeCost;
        Player.RodLevel++;
        Message = $"Joran level {Player.RodLevel}: tali lebih kuat, tarikan lebih cepat.";
        return true;
    }

    public bool BuyNextBoat()
    {
        if (Busy || Player.BoatIndex >= Catalog.Boats.Length - 1) return false;
        var boat = Catalog.Boats[Player.BoatIndex + 1];
        var cost = (int)(boat.Cost * (1 - Character.Discount));
        if (Player.Coins < cost) { Message = $"Butuh {cost:N0} koin untuk {boat.Name}."; return false; }
        Player.Coins -= cost;
        Player.BoatIndex++;
        Message = $"{boat.Name} siap berlayar.";
        return true;
    }

    public bool ClaimQuest(string id)
    {
        var quest = Quests.All.FirstOrDefault(x => x.Id == id);
        if (quest is null || !Quests.Visible(quest, Player) || Player.ClaimedQuests.Contains(id) || Quests.Progress(quest, Player) < quest.Target) return false;
        Player.ClaimedQuests.Add(id);
        Player.Coins += quest.Reward;
        Message = $"Misi selesai: {quest.Title}. +{quest.Reward:N0} koin.";
        return true;
    }

    public static int UnlockLevel(string id) => id switch
    { "ojol" => 2, "satpam" or "bakso" => 3, "kucing" => 4, "robot" => 6, "pendaki" => 8, _ => 1 };

    public bool SelectBait(string id)
    {
        if (Busy || !WorldDefinition.Baits.Any(x => x.Id == id)) return false;
        Player.BaitId = id; Message = $"Umpan dipilih: {Bait.Name}. Biaya {Bait.Cost} koin per lemparan."; return true;
    }

    public bool ActivateAbility()
    {
        if (Player.AbilityCooldown > 0) { Message = $"Kemampuan siap dalam {Math.Ceiling(Player.AbilityCooldown)} detik."; return false; }
        abilitySeconds = 12; Player.AbilityCooldown = 60;
        Tension = Math.Max(0, Tension - .22);
        Message = Character.Id switch
        {
            "bambang" => ResearchReport(),
            "rina" => "Voucher aktif: upgrade joran mendapat diskon tambahan 20%.",
            "tono" => "Berkas Lengkap: akses wilayah satu level lebih tinggi selama 12 detik.",
            "robot" => "Pelacakan otomatis: arah tarikan diselaraskan dengan ikan.",
            "kucing" => "Hoki Oren: peluang tangkapan langka meningkat selama 12 detik.",
            _ => "Kemampuan aktif: tarikan lebih stabil, langkah lebih cepat selama 12 detik."
        };
        return true;
    }

    public void Move(float seconds, Vector2 input)
    {
        if (Busy) return;
        Traversal.Move(Player, World, seconds, input, AbilityActive && Character.Id is "ojan" or "ojol" or "pendaki", jumpSeconds > 0);
        if (Player.OnFoot && World.CanWalk(Traversal.Position(Player), Character.Id is "ojan" or "pendaki", Character.Id == "tono")) safeFootPosition = Traversal.Position(Player);
    }

    public bool ToggleBoarding()
    {
        if (Busy) return false;
        if (Player.InWater) return SelectCatchingMethod("Rod");
        Player.CatchingMethod = CatchingMethod.Rod;
        Player.Climbing = false;
        var point = Traversal.Position(Player);
        if (!Player.OnFoot)
        {
            var landing = new Vector2(-7, 2);
            if (Vector2.Distance(point, new(-3, 5)) > 12 && !World.TryShorePoint(point, Character.Id is "ojan" or "pendaki", out landing, Character.Id == "tono"))
            { Message = "Dekati dermaga atau tepian untuk turun dari perahu."; return false; }
            Player.BoatX = point.X; Player.BoatZ = point.Y;
            Player.OnFoot = true; Traversal.SetPosition(Player, landing); safeFootPosition = landing;
            Message = "Berjalan di dermaga. Kunjungi museum dan aquarium. E untuk naik perahu.";
        }
        else
        {
            if (Vector2.Distance(point, new(-7, 2)) > 5 && Vector2.Distance(point, new(Player.BoatX, Player.BoatZ)) > 5)
            { Message = "Kembali ke ujung dermaga atau dekat perahu untuk naik."; return false; }
            Player.OnFoot = false; Traversal.SetPosition(Player, new(Player.BoatX, Player.BoatZ));
            Message = "Kembali berlayar.";
        }
        return true;
    }

    public bool Jump()
    {
        if (!Player.OnFoot || Busy || jumpSeconds > 0) return false;
        jumpStartHeight = Player.Climbing ? 3 : 0;
        jumpDuration = Player.Climbing || Character.Id == "ojan" ? 1.8 : 1.1;
        jumpSeconds = jumpDuration;
        Player.Climbing = false;
        return true;
    }

    public bool UpgradeExhibition(bool museum)
    {
        if (Busy) return false;
        var level = museum ? Player.MuseumLevel : Player.AquariumLevel;
        var cost = level * 400;
        if (level >= 5 || Player.Coins < cost) { Message = "Upgrade pameran belum tersedia atau koin belum cukup."; return false; }
        Player.Coins -= cost;
        if (museum) Player.MuseumLevel++; else Player.AquariumLevel++;
        Message = "Gedung pameran diperluas. Slot dan kunjungan bertambah."; return true;
    }

    public bool PlaceExhibit(string id, int slot)
    {
        if (Busy || !Player.Exhibits.Contains(id)) return false;
        var kind = Catalog.FindSpecies(id).Kind;
        var museum = kind is CatchKind.Artifact or CatchKind.Junk or CatchKind.Boss;
        var capacity = (museum ? Player.MuseumLevel : Player.AquariumLevel) * 4;
        var offset = museum ? 100 : 0;
        if (slot < offset || slot >= offset + capacity || Player.ExhibitSlots.Any(x => x.Key != id && x.Value == slot)) return false;
        Player.ExhibitSlots[id] = slot;
        Message = $"{Catalog.FindSpecies(id).Name} ditempatkan di {(museum ? "museum" : "aquarium")} slot {slot - offset + 1}."; return true;
    }

    private double BossAttack(bool reeling)
    {
        BossCue = "";
        if (Hooked?.Kind != CatchKind.Boss) return 0;
        var cycle = (sharedEncounterSeconds??fightSeconds) % 9;
        var pattern = World.Biome switch
        {
            Biome.Reef or Biome.Coast or Biome.Crystal => "Semburan tinta",
            Biome.River or Biome.City => "Terjangan buaya",
            Biome.Lake or Biome.ThreeLakes => "Pusaran naga",
            Biome.Pothole or Biome.Quarry => "Hentakan lele",
            _ => "Gelombang dimensi"
        };
        if (cycle < 2) { BossCue = pattern + " • BERSIAP, lepas tarikan saat indikator merah!"; return 0; }
        if (cycle < 4)
        {
            BossCue = pattern + " • SERANGAN!";
            if (World.Biome is Biome.Reef or Biome.Crystal) FishDirection = -FishDirection;
            return reeling ? .24 : 0;
        }
        BossCue = "Boss kelelahan • tarik sekarang.";
        return -.04;
    }
}
