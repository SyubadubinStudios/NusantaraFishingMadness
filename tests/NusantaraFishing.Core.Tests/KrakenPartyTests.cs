using NusantaraFishing.Core;
using NusantaraFishing.Core.Networking;

namespace NusantaraFishing.Core.Tests;

public sealed class KrakenPartyTests
{
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 17, 12, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private static PlayerState ReadyPlayer() => new() { RegionId = "ampat", RodLevel = 3, TotalCaught = 10, Experience = 300 };

    private static (CoopService Service, TestClock Clock, string[] Tokens) Create(Action<CoopAccount>? changePartner = null)
    {
        var tokens = new[] { new string('A', 64), new string('B', 64) };
        var state = new CoopStoredState();
        for (var i = 0; i < 2; i++)
            state.Accounts.Add(new() { Id = "angler-" + i, Room = "kraken", TokenHash = CoopPersistence.HashToken(tokens[i]), Player = ReadyPlayer() });
        changePartner?.Invoke(state.Accounts[1]);
        var clock = new TestClock();
        var service = new CoopService(timeProvider: clock, initialState: state);
        for (var i = 0; i < 2; i++) service.Join(new("Angler", state.Accounts[i].Room, ResumeToken: tokens[i]));
        return (service, clock, tokens);
    }

    [Fact]
    public void LocalKraken_RejectsSoloWithoutSpendingBaitOrStartingFight()
    {
        var game = new GameSession(ReadyPlayer());
        var coins = game.Player.Coins;
        Assert.False(game.Cast(true));
        Assert.Equal(BossRules.PartyRequired, game.Message);
        Assert.Equal(coins, game.Player.Coins);
        Assert.Equal(FishingPhase.Idle, game.Phase);
        Assert.Null(game.Hooked);
        Assert.True(game.Cast());
    }

    [Theory]
    [InlineData("region")]
    [InlineData("room")]
    [InlineData("equipment")]
    [InlineData("experience")]
    [InlineData("swimming")]
    [InlineData("shore")]
    [InlineData("left")]
    [InlineData("timed-out")]
    [InlineData("busy")]
    public void StartKraken_RejectsPartnerWhoCannotParticipate(string reason)
    {
        var (service, clock, tokens) = Create(partner =>
        {
            switch (reason)
            {
                case "region": partner.Player.RegionId = "selatan"; break;
                case "room": partner.Room = "other"; break;
                case "equipment": partner.Player.RodLevel = 2; break;
                case "experience": partner.Player.TotalCaught = 9; break;
                case "swimming": partner.Player.CatchingMethod = CatchingMethod.Snorkel; break;
                case "shore": partner.Player.OnFoot = true; break;
            }
        });
        if (reason == "left") service.Leave(tokens[1]);
        if (reason == "busy") Assert.True(service.Command(tokens[1], new(1, "cast")).Accepted);
        if (reason == "timed-out")
        {
            clock.Now = clock.Now.AddSeconds(29);
            service.GetView(tokens[0]);
            clock.Now = clock.Now.AddSeconds(2);
        }
        var reply = service.Command(tokens[0], new(1, "boss"));
        Assert.False(reply.Accepted);
        Assert.Equal(BossRules.PartyRequired, reply.Error);
        Assert.Null(reply.View.Boss);
        Assert.Equal(FishingPhase.Idle, reply.View.Self.Phase);
        Assert.Equal(300, reply.View.Self.Player.Coins);
    }

    [Fact]
    public void Kraken_WaitsForExplicitParticipation_PausesOnDisconnect_ResumesOnRejoin()
    {
        var (service, clock, tokens) = Create();
        Assert.True(service.Command(tokens[0], new(1, "boss")).Accepted);
        Advance(5);
        var waiting = service.GetView(tokens[0]);
        Assert.Equal(FishingPhase.Waiting, waiting.Self.Phase);
        Assert.Equal(BossRules.WaitingForParty, waiting.Self.BossCue);
        Assert.Equal(waiting.Boss!.Maximum, waiting.Boss.Health);
        Assert.True(service.Command(tokens[1], new(1, "boss")).Accepted);
        Assert.False(service.Command(tokens[0], new(2, "board")).Accepted);
        Advance(5);
        Assert.Equal(FishingPhase.Fighting, service.GetView(tokens[0]).Self.Phase);
        service.Leave(tokens[1]);
        var before = service.GetView(tokens[0]);
        service.Command(tokens[0], new(3, "input", Reel: true));
        Advance(1);
        var paused = service.GetView(tokens[0]);
        Assert.Equal(before.Self.Progress, paused.Self.Progress);
        Assert.Equal(before.Self.Tension, paused.Self.Tension);
        Assert.Equal(before.Boss!.Health, paused.Boss!.Health);
        Assert.Equal(BossRules.WaitingForParty, paused.Boss.Cue);
        Assert.Empty(paused.Self.Player.Inventory);
        service.Join(new("Angler", "kraken", ResumeToken: tokens[1]));
        service.Command(tokens[0], new(4, "input", Reel: true));
        Advance(.1);
        var resumed = service.GetView(tokens[0]);
        Assert.True(resumed.Self.Progress > paused.Self.Progress);
        Assert.NotEqual(BossRules.WaitingForParty, resumed.Boss!.Cue);

        void Advance(double seconds)
        {
            for (var i = 0; i < (int)Math.Round(seconds * 10); i++)
            { clock.Now = clock.Now.AddSeconds(.1); service.Advance(.1); }
        }
    }

    [Fact]
    public void Kraken_WaitingForPartnerStillExpiresWithoutRewards()
    {
        var (service, clock, tokens) = Create();
        Assert.True(service.Command(tokens[0], new(1, "boss")).Accepted);
        for (var i = 0; i < 1802; i++)
        {
            clock.Now = clock.Now.AddSeconds(.1);
            if (i % 100 == 0) service.GetView(tokens[0]);
            service.Advance(.1);
        }
        var view = service.GetView(tokens[0]);
        Assert.Equal(0, view.Boss!.Health);
        Assert.Equal(FishingPhase.Escaped, view.Self.Phase);
        Assert.Empty(view.Self.Player.Inventory);
        Assert.Empty(service.Leaderboard("daily").Entries);
    }
}
