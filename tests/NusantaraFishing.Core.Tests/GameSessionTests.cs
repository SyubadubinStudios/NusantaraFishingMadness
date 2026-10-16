using NusantaraFishing.Core;

namespace NusantaraFishing.Core.Tests;

public sealed class GameSessionTests
{
    private static void PlayToEnd(GameSession game)
    {
        for (var i = 0; i < 1800 && game.Busy; i++)
            game.Tick(.05, game.Phase == FishingPhase.Fighting && game.Tension < .65, Math.Sign(game.FishDirection) * .8);
    }

    [Fact]
    public void Fishing_WithTensionControl_LandsCatchAndAwardsProgress()
    {
        var game = new GameSession(random: new Random(42));
        Assert.True(game.Cast());
        Assert.False(game.Cast());
        PlayToEnd(game);
        Assert.Equal(FishingPhase.Landed, game.Phase);
        Assert.Equal(1, game.Player.TotalCaught);
        Assert.True(game.Player.Experience > 0);
        Assert.Contains(game.Hooked!.Id, game.Player.Discovered);
    }

    [Fact]
    public void Fishing_ConstantReelingAgainstFish_BreaksLineWithoutReward()
    {
        var game = new GameSession(random: new Random(42));
        game.Cast();
        for (var i = 0; i < 2000 && game.Busy; i++) game.Tick(.05, true, -Math.Sign(game.FishDirection));
        Assert.Equal(FishingPhase.Escaped, game.Phase);
        Assert.Empty(game.Player.Inventory);
        Assert.Equal(0, game.Player.Experience);
    }

    [Fact]
    public void Travel_EnforcesLevelAndFunds_AndCannotChangeDuringFishing()
    {
        var game = new GameSession();
        Assert.False(game.Travel("ampat"));
        Assert.Equal(300, game.Player.Coins);
        game.Player.Experience = 150;
        Assert.True(game.Travel("ampat"));
        Assert.Equal(150, game.Player.Coins);
        game.Cast();
        Assert.False(game.Travel("selatan"));
        Assert.Equal("ampat", game.Region.Id);
    }

    [Fact]
    public void Donation_ConsumesOneItem_AndPassiveIncomeDoesNotDuplicateIt()
    {
        var state = new PlayerState();
        state.Inventory.Add(new("lele", 1, 65, DateTimeOffset.UtcNow, "selatan"));
        state.Discovered.Add("lele");
        var game = new GameSession(state);
        Assert.True(game.Donate("lele"));
        Assert.False(game.Donate("lele"));
        for (var i = 0; i < 601; i++) game.Tick(.1);
        Assert.Equal(303, state.Coins);
        Assert.Empty(state.Inventory);
        Assert.Equal(0, game.SellAll());
        Assert.Contains("lele", state.Discovered);
    }

    [Fact]
    public void Quest_CanOnlyBeClaimedOnce_AndOnlyWhenCompleted()
    {
        var game = new GameSession();
        Assert.False(game.ClaimQuest("first"));
        game.Player.TotalCaught = 1;
        Assert.True(game.ClaimQuest("first"));
        Assert.False(game.ClaimQuest("first"));
        Assert.Equal(400, game.Player.Coins);
    }

    [Fact]
    public void Upgrades_ApplyDiscount_AndNeverSpendMissingFunds()
    {
        var game = new GameSession();
        game.SelectCharacter("rina");
        Assert.Equal(200, game.UpgradeCost);
        Assert.True(game.UpgradeRod());
        Assert.Equal(100, game.Player.Coins);
        Assert.False(game.UpgradeRod());
        Assert.False(game.BuyNextBoat());
        Assert.Equal(2, game.Player.RodLevel);
        Assert.Equal(100, game.Player.Coins);
    }

    [Fact]
    public void Boss_RequiresEquipmentAndExperience_AndCanBeDefeated()
    {
        var state = new PlayerState();
        var game = new GameSession(state);
        Assert.False(game.Cast(true));
        state.RodLevel = 5; state.TotalCaught = 10;
        Assert.True(game.Cast(true));
        PlayToEnd(game);
        Assert.Equal(FishingPhase.Landed, game.Phase);
        Assert.Contains("selatan", state.DefeatedBosses);
        Assert.Equal(CatchKind.Boss, game.Hooked!.Kind);
    }

    [Fact]
    public void EveryRegion_HasLocalAndUniversalCatchPool()
    {
        foreach (var region in Catalog.Regions)
        {
            var game = new GameSession(new PlayerState { RegionId = region.Id });
            Assert.Contains(game.AvailableSpecies(), x => x.Id == "lele");
            Assert.All(game.AvailableSpecies(), x => Assert.True(x.Regions.Contains("*") || x.Regions.Contains(region.Id)));
        }
    }

    [Fact]
    public void ConservationEncounter_ReleasesAnimalAndAwardsResearchCoins()
    {
        // Search deterministic seeds, then verify the conservation branch end to end.
        GameSession? selected = null;
        for (var seed = 0; seed < 1000; seed++)
        {
            var game = new GameSession(new PlayerState { RegionId = "ampat", Experience = 150 }, new Random(seed));
            game.Cast();
            if (game.Hooked!.Kind == CatchKind.Conservation) { selected = game; break; }
        }
        Assert.NotNull(selected);
        PlayToEnd(selected);
        Assert.Equal(FishingPhase.Landed, selected.Phase);
        Assert.Empty(selected.Player.Inventory);
        Assert.Equal(360, selected.Player.Coins);
        Assert.Contains(selected.Hooked!.Id, selected.Player.Discovered);
    }

    [Fact]
    public void Simulation_RejectsInvalidTime_AndCapsFrameStalls()
    {
        var game = new GameSession();
        game.Tick(double.NaN); game.Tick(double.PositiveInfinity); game.Tick(-1);
        Assert.Equal(6.5, game.Player.WorldHours);
        game.Tick(1000);
        Assert.InRange(game.Player.WorldHours, 6.5, 6.51);
    }
}
