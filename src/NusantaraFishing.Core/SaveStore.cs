using System.Text.Json;

namespace NusantaraFishing.Core;

public sealed class SaveStore(string path)
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };
    public string Path { get; } = System.IO.Path.GetFullPath(path);

    public PlayerState Load()
    {
        if (!File.Exists(Path)) return new();
        var state = JsonSerializer.Deserialize<PlayerState>(File.ReadAllText(Path), Options)
            ?? throw new InvalidDataException("Save kosong / empty save.");
        Validate(state);
        // Version 1 did not record position, bait, building layouts or regional quest counters.
        // Their property defaults provide a safe harbor spawn and preserve all completed progress.
        state.Version = 2;
        return state;
    }

    public void Save(PlayerState state)
    {
        Validate(state);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(state, Options));
        File.Move(temporary, Path, overwrite: true);
    }

    public static void Validate(PlayerState state)
    {
        if (state.Version is not (1 or 2) || !Catalog.Characters.Any(x => x.Id == state.CharacterId) ||
            !Catalog.Regions.Any(x => x.Id == state.RegionId) || state.Coins < 0 || state.Experience < 0 ||
            state.RodLevel is < 1 or > 10 || state.BoatIndex < 0 || state.BoatIndex >= Catalog.Boats.Length ||
            !double.IsFinite(state.WorldHours) || state.WorldHours is < 0 or >= 24 ||
            !double.IsFinite(state.ExhibitionSeconds) || state.ExhibitionSeconds is < 0 or >= 60 ||
            state.TotalCaught < 0 || !Enum.IsDefined(state.CatchingMethod) || state.Inventory is null || state.Discovered is null || state.Exhibits is null ||
            state.Trophies is null || state.Trophies.Count > 256 ||
            state.Trophies.Any(x => x is null || string.IsNullOrWhiteSpace(x.Period) || x.Period.Length > 64 || x.Rank < 1) ||
            state.Trophies.Select(x => x.Period).Distinct().Count() != state.Trophies.Count ||
            (state.InWater && (state.OnFoot || state.RegionId != "ampat")) ||
            (state.CatchingMethod == CatchingMethod.Hand && (!state.OnFoot || state.CharacterId != "ojan")) ||
            (state.Climbing && (!state.OnFoot || state.CharacterId != "ojan" || state.CatchingMethod != CatchingMethod.Rod)) ||
            state.DefeatedBosses is null || state.ClaimedQuests is null || state.Inventory.Count > 200 ||
            !WorldDefinition.Baits.Any(x => x.Id == state.BaitId) || state.RegionCatches is null || state.ExhibitSlots is null ||
            state.AquariumLevel is < 1 or > 5 || state.MuseumLevel is < 1 or > 5 || state.VisitorsWelcomed < 0 ||
            !float.IsFinite(state.PositionX) || !float.IsFinite(state.PositionZ) ||
            Math.Abs(state.PositionX) > WorldDefinition.Boundary || Math.Abs(state.PositionZ) > WorldDefinition.Boundary ||
            !float.IsFinite(state.BoatX) || !float.IsFinite(state.BoatZ) || !float.IsFinite(state.BoatHeading) ||
            Math.Abs(state.BoatX) > WorldDefinition.Boundary || Math.Abs(state.BoatZ) > WorldDefinition.Boundary ||
            !double.IsFinite(state.AbilityCooldown) || state.AbilityCooldown is < 0 or > 60 ||
            state.RegionCatches.Any(x => x.Value < 0 || !Catalog.Regions.Any(r => r.Id == x.Key)) ||
            state.ExhibitSlots.Values.Distinct().Count() != state.ExhibitSlots.Count ||
            state.ExhibitSlots.Any(x => !state.Exhibits.Contains(x.Key) || !ValidExhibitSlot(state, x.Key, x.Value)))
            throw new InvalidDataException("Save tidak valid atau versinya tidak didukung / invalid or unsupported save.");
        var ids = Catalog.Species.Select(x => x.Id).Concat(Catalog.Regions.Select(x => "boss-" + x.Id)).ToHashSet();
        if (state.Inventory.Any(x => x is null || !ids.Contains(x.SpeciesId) || x.Value < 0 || !double.IsFinite(x.Weight) || x.Weight <= 0 || !Catalog.Regions.Any(r => r.Id == x.RegionId)) ||
            state.Discovered.Any(x => !ids.Contains(x)) || state.Exhibits.Any(x => !ids.Contains(x)) ||
            state.DefeatedBosses.Any(x => !Catalog.Regions.Any(r => r.Id == x)) || state.ClaimedQuests.Any(x => !Quests.All.Any(q => q.Id == x)))
            throw new InvalidDataException("Referensi save tidak dikenal / unknown save reference.");
    }

    private static bool ValidExhibitSlot(PlayerState state, string id, int slot)
    {
        var known = Catalog.Species.Any(x => x.Id == id) || Catalog.Regions.Any(x => "boss-" + x.Id == id);
        if (!known) return false;
        var museum = Catalog.FindSpecies(id).Kind is CatchKind.Artifact or CatchKind.Junk or CatchKind.Boss;
        var offset = museum ? 100 : 0;
        return slot >= offset && slot < offset + (museum ? state.MuseumLevel : state.AquariumLevel) * 4;
    }
}
