using NusantaraFishing.Core;

namespace NusantaraFishing.Core.Tests;

public sealed class DesignAlignmentTests
{
    private static GameSession Encounter(string id, string region = "ampat")
    {
        var game = new GameSession(new PlayerState { RegionId = region, RodLevel = 5, TotalCaught = 12 });
        game.ApplySnapshot(game.Snapshot() with { HookedId = id, Phase = FishingPhase.Fighting, Tension = .2 });
        return game;
    }

    [Fact]
    public void FinalWhale_RequiresAllThreeEndgameBosses_AndPersistsItsOwnDiscovery()
    {
        var game = new GameSession(new PlayerState { RegionId = "dimensi", RodLevel = 5, TotalCaught = 12 });
        Assert.DoesNotContain(game.AvailableSpecies(), x => x.Id == "paus-kubangan");
        game.Player.DefeatedBosses.UnionWith(["gunung", "kristal", "dimensi"]);
        Assert.Contains(game.AvailableSpecies(), x => x.Id == "paus-kubangan");
        game.ApplySnapshot(game.Snapshot() with { HookedId = "paus-kubangan", Phase = FishingPhase.Fighting, Progress = .999 });
        game.Tick(.1, true);
        Assert.Equal(FishingPhase.Landed, game.Phase);
        Assert.Contains("paus-kubangan", game.Player.Discovered);
        Assert.True(game.ClaimQuest("final-whale"));
        Assert.False(game.ClaimQuest("final-whale"));
        SaveStore.Validate(game.Player);
    }

    [Fact]
    public void OptionalBoss_DoesNotGrantRegionalBossCompletion()
    {
        var game = Encounter("penunggu", "kubangan");
        game.ApplySnapshot(game.Snapshot() with { Progress = .999 });
        game.Tick(.1, true);
        Assert.Contains("penunggu", game.Player.Discovered);
        Assert.DoesNotContain("kubangan", game.Player.DefeatedBosses);
    }

    [Fact]
    public void Barter_ConsumesExactItemsAndCannotCreateRepeatedRewards()
    {
        var game = new GameSession();
        for (var i = 0; i < 4; i++) game.Player.Inventory.Add(new("sandal", 1, 25, DateTimeOffset.UtcNow, "selatan"));
        Assert.False(game.Barter("keris"));
        game.SelectCharacter("rina");
        Assert.True(game.Barter("keris"));
        Assert.Single(game.Player.Inventory, x => x.SpeciesId == "sandal");
        Assert.Single(game.Player.Inventory, x => x.SpeciesId == "keris");
        Assert.False(game.Barter("keris"));
        Assert.Equal(300, game.Player.Coins);
        Assert.Equal(0, game.Player.TotalCaught);
        SaveStore.Validate(game.Player);
    }

    [Fact]
    public void SnorkelAndSpear_RespectRegionAndConservation_AndDoNotConsumeBait()
    {
        var game = new GameSession(new PlayerState { RegionId = "ampat", Coins = 0, BaitId = "relic" });
        Assert.True(game.SelectCatchingMethod("Snorkel"));
        Assert.All(game.AvailableSpecies(), x => Assert.Equal(CatchKind.Conservation, x.Kind));
        Assert.True(game.Cast());
        Assert.Equal(0, game.Player.Coins);
        Assert.False(game.SelectCatchingMethod("Spear"));
        game.AbortFishing("test");
        Assert.True(game.SelectCatchingMethod("Spear"));
        Assert.DoesNotContain(game.AvailableSpecies(), x => x.Kind == CatchKind.Conservation);
        Assert.True(game.Travel("selatan"));
        Assert.Equal(CatchingMethod.Rod, game.Player.CatchingMethod);
        Assert.False(game.SelectCatchingMethod("Snorkel"));
    }

    [Fact]
    public void HandCatch_RequiresOjanOnFoot_AndResetsOnCharacterChange()
    {
        var game = new GameSession();
        Assert.False(game.SelectCatchingMethod("Hand"));
        game.SelectCharacter("ojan");
        Assert.False(game.SelectCatchingMethod("Hand"));
        Assert.True(game.ToggleBoarding());
        Assert.True(game.SelectCatchingMethod("Hand"));
        Assert.All(game.AvailableSpecies(), x => Assert.True(x.Kind is CatchKind.Fish or CatchKind.Crustacean or CatchKind.Amphibian));
        Assert.True(game.SelectCharacter("ujang"));
        Assert.Equal(CatchingMethod.Rod, game.Player.CatchingMethod);
    }

    [Fact]
    public void Snorkeling_LeavesBoatBehind_AndRequiresReturningBeforeBoarding()
    {
        var game = new GameSession(new PlayerState { RegionId = "ampat" });
        var boat = new System.Numerics.Vector2(game.Player.BoatX, game.Player.BoatZ);
        Assert.True(game.SelectCatchingMethod("Snorkel"));
        for (var i = 0; i < 30; i++) game.Move(.1f, new(0, 1));
        Assert.Equal(boat, new(game.Player.BoatX, game.Player.BoatZ));
        Assert.False(game.ToggleBoarding());
        Assert.False(game.Cast(true));
        Traversal.SetPosition(game.Player, boat + new System.Numerics.Vector2(1, 0));
        Assert.True(game.ToggleBoarding());
        Assert.False(game.Player.InWater);
        Assert.Equal(boat, Traversal.Position(game.Player));
        SaveStore.Validate(game.Player);
    }

    [Fact]
    public void Ojan_ClimbsOnlyNearAuthoredTree_AndJumpsFromItsHeight()
    {
        var game = new GameSession(new PlayerState { CharacterId = "ojan", OnFoot = true, PositionX = -7, PositionZ = 2 });
        Assert.False(game.ClimbTree());
        var tree = game.World.TreePositions().First();
        Traversal.SetPosition(game.Player, tree);
        Assert.True(game.ClimbTree());
        Assert.True(game.Player.Climbing);
        Assert.False(game.SelectCatchingMethod("Hand"));
        SaveStore.Validate(game.Player);
        var previous = Traversal.Position(game.Player);
        game.Move(.1f, new(1, 0));
        Assert.Equal(previous, Traversal.Position(game.Player));
        Assert.True(game.Jump());
        game.Tick(.1);
        Assert.False(game.Player.Climbing);
        Assert.InRange(game.JumpHeight, 2.8f, 4.5f);
        var client = new GameSession();
        client.ApplySnapshot(game.Snapshot());
        Assert.Equal(game.JumpHeight, client.JumpHeight);
    }

    [Fact]
    public void GovernmentPier_OnlyTonoCanLand_AndSwitchingReturnsToBoat()
    {
        var game = new GameSession(new PlayerState { RegionId = "jakarta", PositionX = 16, PositionZ = 32, BoatX = 16, BoatZ = 32 });
        Assert.False(game.ToggleBoarding());
        Assert.True(game.SelectCharacter("tono"));
        Assert.True(game.ToggleBoarding());
        Assert.True(game.Player.OnFoot);
        Assert.True(WorldDefinition.GovernmentPier.Contains(Traversal.Position(game.Player)));
        game.Tick(.1);
        Assert.True(game.Player.OnFoot);
        Assert.True(game.SelectCharacter("ujang"));
        Assert.False(game.Player.OnFoot);
        Assert.Equal(32, game.Player.PositionZ);
    }

    [Fact]
    public void NinjaReflex_FailsWithoutResponse_AndAllowsAnAlignedResponse()
    {
        var missed = Encounter("ninja");
        for (var i = 0; i < 32; i++) missed.Tick(.1);
        Assert.Equal(FishingPhase.Escaped, missed.Phase);
        Assert.Empty(missed.Player.Inventory);
        var answered = Encounter("ninja");
        for (var i = 0; i < 32; i++) answered.Tick(.1, i >= 14, answered.FishDirection);
        Assert.Equal(FishingPhase.Fighting, answered.Phase);
        Assert.True(answered.Progress > 0);
    }

    [Fact]
    public void CrabCounterWindow_PunishesContinuousReeling()
    {
        var attack = Encounter("kepiting");
        var defend = Encounter("kepiting");
        for (var i = 0; i < 10; i++)
        {
            attack.Tick(.1, true, attack.FishDirection);
            defend.Tick(.1, false, defend.FishDirection);
        }
        Assert.True(attack.Tension > defend.Tension + .25);
        Assert.Contains("Tangkis", defend.BossCue);
    }

    [Fact]
    public void Music_ChangesForRegionBossAndDj_ThenReturnsToRegion()
    {
        var game = Encounter("dj");
        Assert.Equal("dj-theme", MusicSelection.For(game));
        game.ApplySnapshot(game.Snapshot() with { HookedId = "boss-ampat" });
        Assert.Equal("boss-theme", MusicSelection.For(game));
        game.AbortFishing("test");
        Assert.Equal("region-ampat", MusicSelection.For(game));
        game.Player.RegionId = "gua";
        Assert.Equal("region-gua", MusicSelection.For(game));
    }
}
