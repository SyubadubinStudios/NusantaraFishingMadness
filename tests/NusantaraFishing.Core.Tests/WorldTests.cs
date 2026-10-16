using System.Numerics;
using NusantaraFishing.Core;

namespace NusantaraFishing.Core.Tests;

public sealed class WorldTests
{
    [Fact]
    public void IslandSurface_StaysFiniteAndContinuousAcrossCrownAndShore()
    {
        foreach (var island in Worlds.All.SelectMany(x => x.Landmarks).Where(x => x.Kind == LandmarkKind.Island))
            for (var x = -12; x <= 12; x++)
                for (var z = -12; z <= 12; z++)
                {
                    var px = island.X + x * island.Width / 20;
                    var pz = island.Z + z * island.Depth / 20;
                    var height = island.IslandSurface(px, pz, out _);
                    Assert.InRange(height, -.7f, island.Height * 1.2f);
                    Assert.InRange(Math.Abs(height - island.IslandSurface(px + .001f, pz, out _)), 0, .15f);
                    Assert.InRange(Math.Abs(height - island.IslandSurface(px, pz + .001f, out _)), 0, .15f);
                }
    }

    [Fact]
    public void AllRegions_HaveDistinctAuthoredLayoutsAndNavigableSpawn()
    {
        Assert.Equal(Catalog.Regions.Length, Worlds.All.Length);
        Assert.Equal(Worlds.All.Length, Worlds.All.Select(w => string.Join(';', w.Landmarks.Select(x => x.ToString()))).Distinct().Count());
        foreach (var region in Catalog.Regions)
        {
            var world = Worlds.For(region.Id);
            Assert.True(world.CanSail(new(0, 5)), region.Name);
            Assert.True(world.CanWalk(new(-7, 2)));
            Assert.True(world.CanWalk(new(-20, -7)));
            Assert.False(world.CanWalk(new(70, 70)));
            for (var x = -70; x <= 70; x += 7)
                for (var z = -70; z <= 70; z += 7) Assert.InRange(world.DepthAt(x, z), .6f, 12);
        }
    }

    [Fact]
    public void Traversal_SlidesAlongHarborWithoutEnteringSolidGround()
    {
        var state = new PlayerState { PositionX = -3, PositionZ = -6 };
        var world = Worlds.For("selatan");
        for (var i = 0; i < 100; i++) Traversal.Move(state, world, .1f, new(-1, 0), false);
        Assert.True(world.CanSail(Traversal.Position(state)));
        Assert.True(state.PositionX > -4.4f);
        var previous = Traversal.Position(state);
        Traversal.Move(state, world, float.NaN, new(1, 0), false);
        Assert.Equal(previous, Traversal.Position(state));
    }

    [Fact]
    public void HullFits_EveryBoatTierAtEverySpawn()
    {
        foreach (var world in Worlds.All)
            for (var tier = 0; tier < Catalog.Boats.Length; tier++)
                for (var heading = 0f; heading < MathF.Tau; heading += MathF.PI / 4)
                    Assert.True(world.HullFits(new(0, 5), Traversal.Forward(heading), BoatHull.For(tier)), $"{world.RegionId} tier {tier}");
    }

    [Theory]
    [InlineData("selatan", 0, 0, -1, 0)]   // toward the fishing pier
    [InlineData("jakarta", 30, 30, 0, -1)] // toward a flooded building
    [InlineData("kristal", 22, 35, 0, -1)] // toward a crystal spire
    [InlineData("ampat", 12, -10, 0, -1)]  // toward an island beach
    public void Boat_BowNeverEntersSolidScenery(string region, float x, float z, float inputX, float inputZ)
    {
        var world = Worlds.For(region);
        for (var tier = 0; tier < Catalog.Boats.Length; tier++)
        {
            var state = new PlayerState { RegionId = region, BoatIndex = tier, PositionX = x, PositionZ = z, BoatX = x, BoatZ = z };
            var start = Traversal.Position(state);
            for (var i = 0; i < 300; i++) Traversal.Move(state, world, .1f, new(inputX, inputZ), false);
            var hull = BoatHull.For(tier);
            var bow = Traversal.Position(state) + Traversal.Forward(state.BoatHeading) * hull.HalfLength;
            Assert.True(world.CanSail(bow, 0), $"{region} tier {tier} bow at {bow}");
            Assert.True(Vector2.Distance(start, Traversal.Position(state)) > 1, "boat should approach before stopping");
        }
    }

    [Fact]
    public void Boat_CanAlwaysTurnAwayFromAWall()
    {
        var world = Worlds.For("selatan");
        var state = new PlayerState { PositionX = 0, PositionZ = 0, BoatX = 0, BoatZ = 0 };
        for (var i = 0; i < 300; i++) Traversal.Move(state, world, .1f, new(-1, 0), false);
        var pinned = Traversal.Position(state);
        for (var i = 0; i < 10; i++) Traversal.Move(state, world, .1f, new(1, 0), false);
        Assert.True(state.PositionX > pinned.X + 1);
    }

    [Fact]
    public void Boat_OverlappingLegacySaveCanMotorOut()
    {
        var world = Worlds.For("selatan");
        var state = new PlayerState { PositionX = -4.5f, PositionZ = 0, BoatX = -4.5f, BoatZ = 0, BoatHeading = MathF.PI / 2 };
        Assert.False(world.HullFits(Traversal.Position(state), Traversal.Forward(state.BoatHeading), BoatHull.For(0)));
        for (var i = 0; i < 20; i++) Traversal.Move(state, world, .1f, new(1, 0), false);
        Assert.True(world.HullFits(Traversal.Position(state), Traversal.Forward(state.BoatHeading), BoatHull.For(0)));
    }

    [Fact]
    public void Scenery_SolidityMatchesWhatIsRendered()
    {
        Assert.False(Worlds.For("kristal").CanSail(new(22, 19), 0));   // crystal spire
        Assert.False(Worlds.For("gunung").CanSail(new(14, -10), 0));   // vent cone
        Assert.False(Worlds.For("terjun").CanSail(new(0, -29), 0));    // waterfall curtain
        Assert.False(Worlds.For("jakarta").CanSail(new(8, -15), 0));   // sign pole
        Assert.False(Worlds.For("selatan").CanSail(new(-7, 0), 0));    // fishing pier
        Assert.True(Worlds.For("jakarta").CanSail(new(0, -33), .7f));  // under the bridge
        Assert.True(Worlds.For("ampat").CanSail(new(8, 1), .7f));      // over the submerged reef
        Assert.True(Worlds.For("ampat").CanSail(new(48, -12), .7f));   // through the arch
    }

    [Fact]
    public void Currents_ApplyWithoutMovementInput_AndBoardingPreservesBoatPosition()
    {
        var game = new GameSession();
        game.Move(.1f, Vector2.Zero);
        Assert.True(game.Player.PositionX > 0);
        var boat = Traversal.Position(game.Player);
        Assert.True(game.ToggleBoarding());
        Assert.True(game.Player.OnFoot);
        for (var i = 0; i < 4; i++) game.Move(.1f, new(0, -1));
        Assert.True(game.ToggleBoarding());
        Assert.False(game.Player.OnFoot);
        Assert.Equal(boat, Traversal.Position(game.Player));
    }

    [Fact]
    public void ReefCollision_UsesLineSegmentRatherThanOnlyHookPosition()
    {
        var world = Worlds.For("ampat");
        Assert.True(world.LineHitsReef(new(0, 1), new(14, 1)));
        Assert.False(world.LineHitsReef(new(0, 8), new(1, 9)));
    }

    [Fact]
    public void Bait_ChargesOnlyOnSuccessfulCast_AndCannotChangeMidFight()
    {
        var game = new GameSession();
        Assert.True(game.SelectBait("lure"));
        Assert.True(game.Cast());
        Assert.Equal(275, game.Player.Coins);
        Assert.False(game.Cast());
        Assert.False(game.SelectBait("worm"));
        Assert.Equal(275, game.Player.Coins);
        var poor = new GameSession(new PlayerState { Coins = 0, BaitId = "relic" });
        Assert.False(poor.Cast());
        Assert.True(poor.SelectBait("worm"));
        Assert.True(poor.Cast());
    }

    [Fact]
    public void ActiveAbility_CooldownAndCharacterUnlocksCannotBeBypassedBySwitching()
    {
        var game = new GameSession();
        Assert.False(game.SelectCharacter("robot"));
        Assert.True(game.ActivateAbility());
        Assert.False(game.ActivateAbility());
        Assert.True(game.SelectCharacter("rina"));
        Assert.False(game.AbilityActive);
        Assert.False(game.ActivateAbility());
        for (var i = 0; i < 601; i++) game.Tick(.1);
        Assert.True(game.ActivateAbility());
        Assert.Equal(150, game.UpgradeCost);
        game.Player.Experience = 750;
        Assert.True(game.SelectCharacter("robot"));
    }

    [Fact]
    public void Exhibition_PlacementEnforcesOwnershipCapacityAndNoDuplicateSlots()
    {
        var game = new GameSession();
        Assert.False(game.PlaceExhibit("lele", 0));
        game.Player.Exhibits.UnionWith(["lele", "tuna", "keris"]);
        Assert.True(game.PlaceExhibit("lele", 0));
        Assert.False(game.PlaceExhibit("tuna", 0));
        Assert.False(game.PlaceExhibit("tuna", 4));
        Assert.False(game.PlaceExhibit("keris", 1));
        Assert.True(game.PlaceExhibit("keris", 100));
        game.Player.Coins = 1000;
        Assert.True(game.UpgradeExhibition(false));
        Assert.True(game.PlaceExhibit("tuna", 4));
        for (var i = 0; i < 601; i++) game.Tick(.1);
        Assert.True(game.Player.VisitorsWelcomed > 0);
    }

    [Fact]
    public void RegionalQuests_AwardOnlyTheirOwnRegion_AndOnce()
    {
        var game = new GameSession();
        game.Player.RegionCatches["selatan"] = 5;
        Assert.True(game.ClaimQuest("region-selatan"));
        Assert.False(game.ClaimQuest("region-selatan"));
        Assert.False(game.ClaimQuest("region-ampat"));
    }
}
