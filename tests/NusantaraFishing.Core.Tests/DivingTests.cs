using System.Numerics;
using System.Text.Json;
using NusantaraFishing.Core;

namespace NusantaraFishing.Core.Tests;

public sealed class DivingTests
{
    private static GameSession Start(string method)
    {
        var game = new GameSession(new() { RegionId = "ampat", Coins = 0, BaitId = "relic" }, new Random(7));
        Assert.True(game.SelectCatchingMethod(method));
        Assert.True(game.Cast());
        Assert.Equal(FishingPhase.Fighting, game.Phase);
        return game;
    }

    private static void Approach(GameSession game, float distance)
    {
        for (var i = 0; i < 150 && game.DiveRange > distance; i++)
        {
            game.Tick(.05);
            game.Move(.05f, new(0, -1));
        }
        Assert.InRange(game.DiveRange, 1.2f, distance);
    }

    private static void Aim(GameSession game, int frames)
    {
        for (var i = 0; i < frames && game.Busy; i++)
        {
            var target = game.HookPosition - Traversal.Position(game.Player);
            var desired = MathF.Atan2(-target.X, -target.Y);
            var error = MathF.IEEERemainder(desired - game.Dive!.Value.AimYaw, MathF.Tau);
            game.Tick(.05, true, Math.Clamp(-error * 6, -1, 1));
            game.Move(.05f, Vector2.Zero);
        }
    }

    [Fact]
    public void Spear_RequiresApproachAndRelease_ThenUsesProjectileBeforeGrantingCatch()
    {
        var game = Start("Spear");
        var boat = new Vector2(game.Player.BoatX, game.Player.BoatZ);
        var target = game.HookPosition;
        Approach(game, 4);
        Assert.Equal(boat, new(game.Player.BoatX, game.Player.BoatZ));
        Assert.True(Vector2.Distance(target, game.HookPosition) < 1);
        Aim(game, 40);
        Assert.Equal(FishingPhase.Fighting, game.Phase);
        Assert.Equal(DiveStage.Aim, game.Dive!.Value.Stage);
        Assert.Empty(game.Player.Inventory);
        game.Tick(.05, false);
        Assert.Equal(DiveStage.Projectile, game.Dive!.Value.Stage);
        Assert.Empty(game.Player.Inventory);
        for (var i = 0; i < 20 && game.Busy; i++) game.Tick(.05);
        Assert.Equal(FishingPhase.Landed, game.Phase);
        Assert.Single(game.Player.Inventory);
        Assert.Equal(1, game.Player.TotalCaught);
        Assert.Equal(0, game.Player.Coins);
        for (var i = 0; i < 40; i++) game.Tick(.05, true);
        Assert.Single(game.Player.Inventory);
    }

    [Fact]
    public void Spear_ThrownFromTooFarMissesWithoutReward()
    {
        var game = Start("Spear");
        Aim(game, 20);
        game.Tick(.05, false);
        for (var i = 0; i < 20 && game.Busy; i++) game.Tick(.05);
        Assert.Equal(FishingPhase.Escaped, game.Phase);
        Assert.Equal(DiveStage.Miss, game.Dive!.Value.Stage);
        Assert.Empty(game.Player.Inventory);
        Assert.Equal(0, game.Player.TotalCaught);
    }

    [Fact]
    public void Snorkel_RequiresRangeAndFocus_RecordsAndReleasesProtectedSpecies()
    {
        var game = Start("Snorkel");
        Aim(game, 40);
        Assert.Equal(0, game.Progress);
        Assert.Equal(0, game.Player.TotalCaught);
        Approach(game, 3);
        Aim(game, 60);
        Assert.Equal(FishingPhase.Landed, game.Phase);
        Assert.Equal(CatchKind.Conservation, game.Hooked!.Kind);
        Assert.Contains(game.Hooked.Id, game.Player.Discovered);
        Assert.Empty(game.Player.Inventory);
        Assert.Equal(60, game.Player.Coins);
        Assert.Equal(1, game.Player.TotalCaught);
    }

    [Fact]
    public void Aim_MustPointAtTarget_AndHorizontalInputTurnsInsteadOfStrafing()
    {
        var game = Start("Snorkel");
        Approach(game, 3);
        for (var i = 0; i < 25; i++) game.Tick(.05, true, 1);
        var before = Traversal.Position(game.Player);
        game.Move(.1f, new(1, 0));
        Assert.True(Vector2.Distance(before, Traversal.Position(game.Player)) < .1f);
        Assert.False(game.DiveAligned);
        Assert.Equal(0, game.Progress);
        Assert.True(game.Busy);
    }

    [Fact]
    public void Reef_ObstructsAimAndStopsProjectile()
    {
        var game = Start("Spear");
        var reef = game.World.Landmarks.First(x => x.Kind == LandmarkKind.Reef);
        var radius = Math.Min(reef.Width, reef.Depth) / 2;
        Traversal.SetPosition(game.Player, new(reef.X, reef.Z + radius + .6f));
        var dive = game.Dive!.Value with { OriginX = reef.X, OriginZ = reef.Z - radius - 1,
            TargetX = reef.X, TargetZ = reef.Z - radius - 1, AimYaw = 0, Charge = 1, TriggerHeld = true };
        game.ApplySnapshot(game.Snapshot() with { Dive = dive });
        Assert.False(game.DiveClear);
        game.Tick(.05, false);
        for (var i = 0; i < 20 && game.Busy; i++) game.Tick(.05);
        Assert.Equal(FishingPhase.Escaped, game.Phase);
        Assert.Contains("karang", game.Message);
        Assert.Empty(game.Player.Inventory);
    }

    [Fact]
    public void Dive_CanCancelOrTimeOut_AndSnapshotPreservesAuthoritativeGeometry()
    {
        var game = Start("Snorkel");
        var snapshot = JsonSerializer.Deserialize<SimulationSnapshot>(JsonSerializer.Serialize(game.Snapshot()))!;
        var client = new GameSession();
        client.ApplySnapshot(snapshot);
        Assert.Equal(game.HookPosition, client.HookPosition);
        Assert.Equal(game.Dive, client.Dive);
        Assert.Throws<InvalidDataException>(() => client.ApplySnapshot(snapshot with { Dive = snapshot.Dive!.Value with { TargetX = float.NaN } }));
        Assert.True(game.CancelDive());
        Assert.Null(game.Dive);
        Assert.False(game.Busy);
        Assert.True(game.Cast());
        for (var i = 0; i < 602; i++) game.Tick(.1);
        Assert.Equal(FishingPhase.Escaped, game.Phase);
        Assert.Empty(game.Player.Inventory);
    }
}
