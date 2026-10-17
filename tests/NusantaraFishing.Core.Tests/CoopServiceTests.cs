using NusantaraFishing.Core;
using NusantaraFishing.Core.Networking;

namespace NusantaraFishing.Core.Tests;

public sealed class CoopServiceTests
{
    private sealed class TestClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; }=new(2026,10,15,12,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow()=>Now;
        public void Advance(double seconds)=>Now=Now.AddSeconds(seconds);
    }
    private static NetworkRuleException Rule(Action action,int status)
    { var error=Assert.Throws<NetworkRuleException>(action); Assert.Equal(status,error.StatusCode); return error; }

    [Fact]
    public void CoopRoom_AdmitsFourClients_RejectsFifth_AndResumesSameIdentity()
    {
        var service=new CoopService();
        var players=Enumerable.Range(1,4).Select(i=>service.Join(new("Angler "+i))).ToArray();
        Assert.Equal(4,service.GetView(players[0].Token).Players.Length);
        Rule(()=>service.Join(new("Fifth")),409);
        service.Leave(players[3].Token);
        var resumed=service.Join(new("Angler 4",ResumeToken:players[3].Token));
        Assert.Equal(players[3].PlayerId,resumed.PlayerId);
        Assert.Equal(4,resumed.View.Players.Length);
    }

    [Fact]
    public void Join_RejectsIncompatibleVersionsAndInvalidRoomNames()
    {
        var service=new CoopService();
        Rule(()=>service.Join(new("Client",Protocol:999)),426);
        Rule(()=>service.Join(new("Client",Room:"../other")),400);
        Rule(()=>service.Join(new("\nname")),400);
    }

    [Fact]
    public void ServerSnapshot_CannotBeMutatedToGrantLocalWealth()
    {
        var service=new CoopService();
        var join=service.Join(new("Client"));
        join.View.Self.Player.Coins=999999;
        join.View.Self.Player.Inventory.Add(new("lele",1,999999,DateTimeOffset.UtcNow,"selatan"));
        var actual=service.GetView(join.Token);
        Assert.Equal(300,actual.Self.Player.Coins);
        Assert.Empty(actual.Self.Player.Inventory);
    }

    [Fact]
    public void SwimmingAndBarter_AreServerValidatedAndVisibleToPeers()
    {
        const string token = "EEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEEE";
        var seed = new CoopStoredState
        {
            Accounts = [new() { Id = "swimmer", Room = "reef", TokenHash = CoopPersistence.HashToken(token),
                Player = new() { RegionId = "ampat", CharacterId = "rina" } }]
        };
        for (var i = 0; i < 3; i++)
            seed.Accounts[0].Player.Inventory.Add(new("sandal", 1, 5, DateTimeOffset.UtcNow, "ampat"));
        var service = new CoopService(initialState: seed);
        service.Join(new("Rina", "reef", ResumeToken: token));
        var observer = service.Join(new("Observer", "reef"));
        Assert.True(service.Command(token, new(1, "method", "Snorkel")).Accepted);
        var peer = Assert.Single(service.GetView(observer.Token).Players, x => x.Id == "swimmer");
        Assert.True(peer.InWater);
        Assert.False(peer.OnFoot);
        Assert.Equal(0, peer.BoatX);
        Assert.Equal(1.5f, peer.X - peer.BoatX);
        Assert.False(service.Command(token, new(2, "method", "Hand")).Accepted);
        Assert.True(service.Command(token, new(3, "method", "Rod")).Accepted);
        Assert.True(service.Command(token, new(4, "barter", "keris")).Accepted);
        Assert.False(service.Command(token, new(5, "barter", "keris")).Accepted);
        Assert.Equal("keris", Assert.Single(service.GetView(token).Self.Player.Inventory).SpeciesId);
    }

    [Fact]
    public void Commands_RejectReplaysAndNeverApplyUpgradeTwice()
    {
        var service=new CoopService();
        var join=service.Join(new("Client"));
        Assert.True(service.Command(join.Token,new(1,"rod")).Accepted);
        var replay=service.Command(join.Token,new(1,"rod"));
        Assert.False(replay.Accepted);
        Assert.Equal(50,replay.View.Self.Player.Coins);
        Assert.Equal(2,replay.View.Self.Player.RodLevel);
        Rule(()=>service.Command(join.Token,new(2,"grant-coins")),400);
        Rule(()=>service.Command("wrong",new(3,"input")),401);
    }

    [Fact]
    public void Movement_IsServerTimed_AndStaleInputStopsSailing()
    {
        var clock=new TestClock();
        var service=new CoopService(timeProvider:clock);
        var join=service.Join(new("Client"));
        service.Command(join.Token,new(1,"input",X:1));
        service.Advance(.1);
        var active=service.GetView(join.Token).Self.Player.PositionX;
        Assert.InRange(active,.29f,.4f);
        clock.Advance(3);
        service.Advance(.1);
        var after=service.GetView(join.Token).Self.Player.PositionX;
        Assert.InRange(after-active,0,.03f);
        Rule(()=>service.Command(join.Token,new(2,"input",X:2)),400);
        Assert.Throws<ArgumentOutOfRangeException>(()=>service.Advance(5));
    }

    [Fact]
    public void RequestRateLimit_IsPerIdentity_AndDoesNotAffectAnotherPlayer()
    {
        var clock=new TestClock();
        var service=new CoopService(timeProvider:clock);
        var first=service.Join(new("First")); var second=service.Join(new("Second"));
        for(var i=1;i<=40;i++) service.Command(first.Token,new(i,"input"));
        Rule(()=>service.Command(first.Token,new(41,"input")),429);
        Assert.True(service.Command(second.Token,new(1,"input")).Accepted);
        clock.Advance(1.1);
        Assert.True(service.Command(first.Token,new(41,"input")).Accepted);
    }

    [Fact]
    public void Festival_UsesLargerCapacityAndCannotChangeAnExistingRoomsMode()
    {
        var service=new CoopService();
        for(var i=0;i<31;i++) service.Join(new("Visitor "+i,"festival",RoomMode.Festival));
        var joined=service.Join(new("Visitor 32","festival",RoomMode.Festival));
        Assert.Equal(32,joined.View.Players.Length);
        Rule(()=>service.Join(new("Visitor 33","festival",RoomMode.Festival)),409);
        Rule(()=>service.Join(new("Wrong mode","festival",RoomMode.Coop)),409);
    }

    [Theory]
    [InlineData("selatan", 4)]
    [InlineData("ampat", 4)]
    [InlineData("ampat", 2)]
    public void Party_DefeatsSharedBoss_AndReconnectRestartCannotDuplicateRewards(string region, int playerCount)
    {
        var clock=new TestClock();
        var folder=Path.Combine(Path.GetTempPath(),"nfm-coop-"+Guid.NewGuid());
        var path=Path.Combine(folder,"server.json");
        var seed=new CoopStoredState();
        var tokens=Enumerable.Range(1,playerCount).Select(i=>i.ToString("X64")).ToArray();
        for(var i=0;i<playerCount;i++) seed.Accounts.Add(new()
        {
            Id="player-"+i,Name="Angler "+i,Room="boss-test",TokenHash=CoopPersistence.HashToken(tokens[i]),
            Player=new() { RegionId=region,Coins=1000,Experience=300,RodLevel=3,TotalCaught=10 }
        });
        try
        {
            var service=new CoopService(path,clock,seed);
            var views=new RoomView[playerCount]; var sequence=new long[playerCount];
            for(var i=0;i<playerCount;i++)
                views[i]=service.Join(new("Angler "+i,"boss-test",ResumeToken:tokens[i])).View;
            for(var i=0;i<playerCount;i++)
            {
                Assert.True(service.Command(tokens[i],new(++sequence[i],"boss")).Accepted);
            }
            var won=false;
            for(var frame=0;frame<3000;frame++)
            {
                clock.Advance(.05);
                for(var i=0;i<playerCount;i++)
                {
                    var self=views[i].Self;
                    var input=new PlayerCommand(++sequence[i],"input",Reel:self.Phase==FishingPhase.Fighting && self.Tension<.6 && !self.BossCue.Contains("SERANGAN",StringComparison.Ordinal),Direction:(float)Math.Sign(self.FishDirection)*.8f);
                    views[i]=service.Command(tokens[i],input).View;
                    if(views[i].Boss is { Health:>0 } && views[i].Self.Phase is FishingPhase.Landed or FishingPhase.Escaped)
                        views[i]=service.Command(tokens[i],new(++sequence[i],"boss")).View;
                }
                service.Advance(.05);
                if(views[0].Boss is { Health:<=0 }) { won=true; break; }
            }
            Assert.True(won,"The party must complete the shared encounter.");
            for(var i=0;i<playerCount;i++)
            {
                var view=service.GetView(tokens[i]);
                Assert.Single(view.Self.Player.Inventory);
                Assert.Equal("boss-"+region,view.Self.Player.Inventory[0].SpeciesId);
                Assert.Equal(11,view.Self.Player.TotalCaught);
                Assert.Equal(playerCount,view.Boss!.Contributors);
            }
            service.Leave(tokens[playerCount - 1]);
            var resumed=service.Join(new("Angler 3","boss-test",ResumeToken:tokens[playerCount - 1]));
            Assert.Single(resumed.View.Self.Player.Inventory);
            service.Flush();
            var restarted=new CoopService(path,clock);
            var afterRestart=restarted.Join(new("Angler 3","boss-test",ResumeToken:tokens[playerCount - 1]));
            Assert.Single(afterRestart.View.Self.Player.Inventory);
            Assert.False(restarted.Command(tokens[playerCount - 1],new(afterRestart.LastSequence,"boss")).Accepted);
            Assert.Single(restarted.GetView(tokens[playerCount - 1]).Self.Player.Inventory);
            Assert.Equal(playerCount,restarted.Leaderboard("daily").Entries.Length);
        }
        finally { if(Directory.Exists(folder)) Directory.Delete(folder,true); }
    }

    [Fact]
    public void SharedBoss_ExpiresWithoutLootOrTournamentPoints_AndUsesOneClock()
    {
        var clock=new TestClock();
        var tokens=new[]{new string('A',64),new string('B',64)};
        var seed=new CoopStoredState();
        for(var i=0;i<2;i++) seed.Accounts.Add(new()
        {
            Id="timeout-"+i,Name="Angler "+i,Room="timeout",TokenHash=CoopPersistence.HashToken(tokens[i]),
            Player=new() { Experience=300,RodLevel=3,TotalCaught=10 }
        });
        var service=new CoopService(timeProvider:clock,initialState:seed);
        foreach(var token in tokens)
        {
            service.Join(new("Angler","timeout",ResumeToken:token));
            Assert.True(service.Command(token,new(1,"boss")).Accepted);
        }
        for(var frame=0;frame<3602;frame++)
        {
            clock.Advance(.05);
            service.Advance(.05);
            if(frame%20==0)
            {
                var first=service.GetView(tokens[0]);
                var second=service.GetView(tokens[1]);
                Assert.Equal(first.Boss!.Cue,second.Boss!.Cue);
            }
        }
        foreach(var token in tokens)
        {
            var view=service.GetView(token);
            Assert.Equal(0,view.Boss!.Health);
            Assert.Empty(view.Self.Player.Inventory);
            Assert.Equal(10,view.Self.Player.TotalCaught);
            Assert.Equal(FishingPhase.Escaped,view.Self.Phase);
        }
        Assert.Empty(service.Leaderboard("daily").Entries);
    }

    [Fact]
    public void SharedBoss_NonContributorCannotReceiveLootOrScoreAfterDefeat()
    {
        var clock=new TestClock();
        var tokens=new[]{new string('C',64),new string('D',64)};
        var seed=new CoopStoredState();
        for(var i=0;i<2;i++) seed.Accounts.Add(new()
        {
            Id="contributor-"+i,Name="Angler "+i,Room="contribution",TokenHash=CoopPersistence.HashToken(tokens[i]),
            Player=new() { Experience=300,RodLevel=3,TotalCaught=10 }
        });
        seed.Bosses.Add(new() { Room="contribution",Maximum=200,Health=1,Contributions=new() { ["contributor-0"]=0,["contributor-1"]=0 } });
        var service=new CoopService(timeProvider:clock,initialState:seed);
        foreach(var token in tokens) service.Join(new("Angler","contribution",ResumeToken:token));
        Assert.True(service.Command(tokens[0],new(1,"boss")).Accepted);
        long sequence=1;
        for(var frame=0;frame<2400;frame++)
        {
            clock.Advance(.05);
            var view=service.GetView(tokens[0]);
            if(view.Boss!.Health<=0) break;
            var self=view.Self;
            service.Command(tokens[0],new(++sequence,"input",Reel:self.Phase==FishingPhase.Fighting && self.Tension<.6 && !self.BossCue.Contains("SERANGAN",StringComparison.Ordinal),Direction:(float)Math.Sign(self.FishDirection)*.8f));
            if(self.Phase is FishingPhase.Escaped or FishingPhase.Landed) service.Command(tokens[0],new(++sequence,"boss"));
            if(frame%20==0) service.GetView(tokens[1]);
            service.Advance(.05);
        }
        Assert.Single(service.GetView(tokens[0]).Self.Player.Inventory);
        var passive=service.GetView(tokens[1]);
        Assert.Empty(passive.Self.Player.Inventory);
        Assert.Equal(10,passive.Self.Player.TotalCaught);
        Assert.Equal(FishingPhase.Escaped,passive.Self.Phase);
        Assert.Single(service.Leaderboard("daily").Entries);
        Assert.Equal("contributor-0",service.Leaderboard("daily").Entries[0].PlayerId);
    }

    [Theory]
    [InlineData("daily","daily:2026-10-15",1)]
    [InlineData("weekly","weekly:2026-W42",7)]
    [InlineData("seasonal","seasonal:2026-Q4",92)]
    public void TournamentPeriods_UseUtcBoundaries(string cadence,string expected,int days)
    {
        var period=TournamentPeriods.Get(cadence,new(2026,10,15,12,0,0,TimeSpan.Zero));
        Assert.Equal(expected,period.Key);
        Assert.Equal(days,(period.End-period.Start).TotalDays);
    }

    [Fact]
    public void TournamentRewards_RequireEndedParticipation_AndCanOnlyBeClaimedOnce()
    {
        var clock=new TestClock();
        var yesterday=TournamentPeriods.Get("daily",clock.Now,true);
        const string token="AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
        var seed=new CoopStoredState
        {
            Accounts=[new() { Id="one",Room="test",TokenHash=CoopPersistence.HashToken(token) }],
            Scores=[new() { PlayerId="one",Name="Angler",Period=yesterday.Key,StartsAt=yesterday.Start,EndsAt=yesterday.End,Score=100,Catches=1,LargestWeight=2 }]
        };
        var service=new CoopService(timeProvider:clock,initialState:seed);
        service.Join(new("Angler","test",ResumeToken:token));
        Assert.True(service.Command(token,new(1,"tournament","daily")).Accepted);
        Assert.Equal(1300,service.GetView(token).Self.Player.Coins);
        Assert.False(service.Command(token,new(2,"tournament","daily")).Accepted);
        Assert.False(service.Command(token,new(3,"tournament","weekly")).Accepted);
        var trophy = Assert.Single(service.GetView(token).Self.Player.Trophies);
        Assert.Equal(1, trophy.Rank);
        Assert.Equal(yesterday.Key, trophy.Period);
    }
}
