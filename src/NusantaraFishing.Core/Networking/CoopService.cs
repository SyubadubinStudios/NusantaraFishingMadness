using System.Numerics;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace NusantaraFishing.Core.Networking;

/// <summary>Authoritative, serialized room simulation. Network transports never accept player state or rewards.</summary>
public sealed class CoopService
{
    private sealed class Session(CoopAccount account)
    {
        public CoopAccount Account { get; }=account;
        public GameSession Game { get; }=new(account.Player);
        public DateTimeOffset LastSeen { get; set; }=DateTimeOffset.MinValue;
        public DateTimeOffset LastInput { get; set; }=DateTimeOffset.MinValue;
        public PlayerCommand Input { get; set; }=new(0,"input");
        public DateTimeOffset RateWindow { get; set; }=DateTimeOffset.MinValue;
        public int Requests { get; set; }
    }
    private readonly object gate=new();
    private readonly CoopStoredState state;
    private readonly Dictionary<string,Session> sessions;
    private readonly TimeProvider clock;
    private readonly string? path;
    private double secondsSinceSave;
    public CoopService(string? persistencePath=null,TimeProvider? timeProvider=null,CoopStoredState? initialState=null)
    {
        path=persistencePath; clock=timeProvider??TimeProvider.System;
        state=initialState??(path is null?new():CoopPersistence.Load(path));
        sessions=state.Accounts.ToDictionary(x=>x.TokenHash,x=>new Session(x));
    }
    private bool Active(Session session)=>(clock.GetUtcNow()-session.LastSeen).TotalSeconds<=30;
    private Session Auth(string token,bool rate=true)
    {
        if(token.Length!=64 || token.Any(x=>!Uri.IsHexDigit(x)) || !sessions.TryGetValue(CoopPersistence.HashToken(token),out var session))
            throw new NetworkRuleException("Session token is invalid. Rejoin with the saved server identity.",401);
        if(rate)
        {
            var now=clock.GetUtcNow();
            if((now-session.RateWindow).TotalSeconds>=1) { session.RateWindow=now; session.Requests=0; }
            if(++session.Requests>40) throw new NetworkRuleException("Too many requests. Wait before retrying.",429);
        }
        if(!Active(session)) throw new NetworkRuleException("Session disconnected. Resume using Join.",401);
        session.LastSeen=clock.GetUtcNow();
        return session;
    }
    public JoinReply Join(JoinRequest request)
    {
        lock(gate)
        {
            if(request.Protocol!=CoopProtocol.Version) throw new NetworkRuleException("Protocol version mismatch. Update the client.",426);
            if(request.Name is null || string.IsNullOrWhiteSpace(request.Name) || request.Name.Length>24 || request.Name.Any(char.IsControl) ||
                request.Room is null || !Regex.IsMatch(request.Room,"^[a-z0-9-]{1,24}$",RegexOptions.CultureInvariant) || !Enum.IsDefined(request.Mode))
                throw new NetworkRuleException("Use a name up to 24 characters and a room ID containing lowercase letters, digits or hyphens.");
            Session session;
            string token;
            if(request.ResumeToken is { Length:>0 } resume)
            {
                if(resume.Length!=64 || !sessions.TryGetValue(CoopPersistence.HashToken(resume),out session!))
                    throw new NetworkRuleException("Resume token is unknown.",401);
                token=resume;
                if(session.Account.Room!=request.Room || session.Account.Mode!=request.Mode)
                    throw new NetworkRuleException("This identity belongs to a different room.",409);
            }
            else
            {
                if(state.Accounts.Count>=1024) throw new NetworkRuleException("This server has reached its profile capacity.",503);
                token=Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
                var account=new CoopAccount { Name=request.Name.Trim(),Room=request.Room,Mode=request.Mode,TokenHash=CoopPersistence.HashToken(token) };
                session=new(account);
            }
            var capacity=request.Mode==RoomMode.Coop?CoopProtocol.CoopCapacity:CoopProtocol.FestivalCapacity;
            var active=sessions.Values.Count(x=>x.Account.Room==request.Room && Active(x) && x.Account.Id!=session.Account.Id);
            if(active>=capacity) throw new NetworkRuleException("The room is full. Try another room.",409);
            if(sessions.Values.Any(x=>x.Account.Room==request.Room && x.Account.Mode!=request.Mode))
                throw new NetworkRuleException("The room already uses a different mode.",409);
            if(!sessions.ContainsKey(session.Account.TokenHash))
            { sessions.Add(session.Account.TokenHash,session); state.Accounts.Add(session.Account); }
            session.LastSeen=clock.GetUtcNow(); session.LastInput=DateTimeOffset.MinValue;
            session.Input=new(session.Account.LastSequence,"input");
            Persist();
            return new(token,session.Account.Id,session.Account.LastSequence,View(session));
        }
    }
    public RoomView GetView(string token)
    { lock(gate) return View(Auth(token)); }
    public void Leave(string token)
    {
        lock(gate)
        {
            var session=Auth(token); session.LastSeen=DateTimeOffset.MinValue; session.Input=new(0,"input"); Persist();
        }
    }
    public CommandReply Command(string token,PlayerCommand command)
    {
        lock(gate)
        {
            var session=Auth(token);
            if(command.Sequence<=session.Account.LastSequence) return new(false,"Duplicate or stale command ignored.",View(session));
            if(command.Sequence<=0 || command.Sequence-session.Account.LastSequence>10000 ||
                !float.IsFinite(command.X) || !float.IsFinite(command.Z) || !float.IsFinite(command.Direction) ||
                Math.Abs(command.X)>1 || Math.Abs(command.Z)>1 || Math.Abs(command.Direction)>1 || command.Target?.Length>128)
                throw new NetworkRuleException("Invalid command input.");
            var target=command.Target??"";
            var game=session.Game;
            var boss=CurrentBoss(session);
            var participating=boss is { Health:>0 } && boss.Contributions.ContainsKey(session.Account.Id);
            var accepted=command.Kind switch
            {
                "input"=>SetInput(session,command),
                "cast"=>!participating && game.Cast(),
                "cancel-dive"=>game.CancelDive(),
                "boss"=>StartBoss(session),
                "sell"=>Sell(game),
                "rod"=>game.UpgradeRod(),
                "boat"=>game.BuyNextBoat(),
                "character"=>game.SelectCharacter(target),
                "travel"=>!participating && game.Travel(target),
                "bait"=>game.SelectBait(target),
                "method"=>!participating && game.SelectCatchingMethod(target),
                "barter"=>game.Barter(target),
                "climb"=>game.ClimbTree(),
                "ability"=>game.ActivateAbility(),
                "board"=>!participating && game.ToggleBoarding(),
                "jump"=>game.Jump(),
                "donate"=>game.Donate(target),
                "place"=>game.PlaceExhibit(target,command.Slot),
                "aquarium"=>game.UpgradeExhibition(false),
                "museum"=>game.UpgradeExhibition(true),
                "quest"=>game.ClaimQuest(target),
                "tournament"=>ClaimTournament(session,target),
                _=>throw new NetworkRuleException("Unknown command kind.")
            };
            session.Account.LastSequence=command.Sequence;
            if(command.Kind!="input") Persist();
            return new(accepted,accepted?null:game.Message,View(session));
        }
    }
    private bool SetInput(Session session,PlayerCommand command)
    { session.Input=command; session.LastInput=clock.GetUtcNow(); return true; }
    private static bool Sell(GameSession game)
    { if(game.Busy) return false; game.SellAll(); return true; }
    private CoopBossState? CurrentBoss(Session session)=>state.Bosses.FirstOrDefault(x=>x.Room==session.Account.Room && x.Region==session.Game.Region.Id);
    private bool ReadyForBoss(Session member, string room, string region) =>
        Active(member) && member.Account.Room == room && member.Game.Region.Id == region &&
        member.Game.Player.RodLevel >= 3 && member.Game.Player.TotalCaught >= 10 &&
        !member.Game.Player.OnFoot && !member.Game.Player.InWater &&
        member.Game.Player.Inventory.Count < 200 && member.Game.Player.Coins >= member.Game.Bait.Cost &&
        (!member.Game.Busy || member.Game.BossRewardsDeferred);

    private bool WaitingForParty(CoopBossState boss) => BossRules.MinimumPlayers(boss.Region) > 1 &&
        sessions.Values.Count(x => Active(x) && x.Account.Room == boss.Room && x.Game.Region.Id == boss.Region &&
            !x.Game.Player.OnFoot && !x.Game.Player.InWater && boss.Contributions.ContainsKey(x.Account.Id)) < BossRules.MinimumPlayers(boss.Region);

    private bool StartBoss(Session session)
    {
        var game=session.Game;
        var boss=CurrentBoss(session);
        if(game.Busy) return false;
        if(boss is { Health:<=0 } && state.ElapsedSeconds-boss.CompletedAt<60)
            return game.RejectBoss("Boss sedang beristirahat. Tunggu hingga 60 detik sebelum menantang lagi.");
        var readyPlayers = sessions.Values.Count(x => ReadyForBoss(x, session.Account.Room, game.Region.Id));
        if (BossRules.MinimumPlayers(game.Region.Id) > 1 &&
            (!ReadyForBoss(session, session.Account.Room, game.Region.Id) || readyPlayers < BossRules.MinimumPlayers(game.Region.Id)))
            return game.RejectBoss(BossRules.PartyRequired);
        if(!game.CastSharedBoss(Math.Max(1, readyPlayers))) return false;
        if(boss is null || boss.Health<=0)
        {
            if(boss is not null) state.Bosses.Remove(boss);
            var count=sessions.Values.Count(x=>Active(x) && x.Account.Room==session.Account.Room && x.Game.Region.Id==game.Region.Id);
            boss=new() { Room=session.Account.Room,Region=game.Region.Id,Maximum=Math.Max(1,count)*100,Health=Math.Max(1,count)*100,StartedAt=state.ElapsedSeconds };
            state.Bosses.Add(boss);
        }
        if(!boss.Contributions.ContainsKey(session.Account.Id) && boss.Contributions.Count>=boss.Maximum/100)
        { boss.Maximum+=100; boss.Health+=100; }
        boss.Contributions.TryAdd(session.Account.Id,0);
        return true;
    }
    public void Advance(double seconds)
    {
        if(!double.IsFinite(seconds) || seconds<=0 || seconds>.1) throw new ArgumentOutOfRangeException(nameof(seconds));
        lock(gate)
        {
            state.ElapsedSeconds+=seconds;
            state.WorldHours=(state.WorldHours+seconds/30)%24;
            var weather=(Weather)((int)(state.ElapsedSeconds/75)%5);
            var save=false;
            foreach(var expired in state.Bosses.Where(x=>x.Health>0 && state.ElapsedSeconds-x.StartedAt>=180))
            {
                expired.Health=0; expired.Expired=true; expired.Rewarded=true; expired.CompletedAt=state.ElapsedSeconds;
                foreach(var member in sessions.Values.Where(x=>x.Account.Room==expired.Room && x.Game.Region.Id==expired.Region && x.Game.Hooked?.Kind==CatchKind.Boss))
                    member.Game.AbortFishing("Waktu encounter habis. Boss kembali ke kedalaman.");
                save=true;
            }
            foreach(var session in sessions.Values.Where(Active))
            {
                var input=(clock.GetUtcNow()-session.LastInput).TotalSeconds<=2?session.Input:new(0,"input");
                var phase=session.Game.Phase;
                session.Game.SynchronizeEnvironment(state.WorldHours,weather);
                var shared=CurrentBoss(session);
                session.Game.SynchronizeBossClock(shared is { Health:>0 }?state.ElapsedSeconds-shared.StartedAt:null);
                session.Game.SetBossPartyWaiting(shared is { Health:>0 } &&
                    shared.Contributions.ContainsKey(session.Account.Id) && WaitingForParty(shared));
                session.Game.Tick(seconds,input.Reel,input.Direction);
                session.Game.Move((float)seconds,new(input.X,input.Z));
                if(phase==FishingPhase.Fighting && session.Game.Phase==FishingPhase.Landed)
                {
                    var boss=CurrentBoss(session);
                    if(session.Game.Hooked?.Kind==CatchKind.Boss && boss is { Health:>0 } && boss.Contributions.ContainsKey(session.Account.Id))
                    {
                        var damage=40+session.Game.Player.RodLevel*15;
                        boss.Health=Math.Max(0,boss.Health-damage);
                        boss.Contributions[session.Account.Id]+=damage;
                        if(boss.Health<=0) FinishBoss(boss);
                    }
                    else if(!session.Game.BossRewardsDeferred && session.Game.LastCatch is { } caught) Score(session.Account,caught);
                    save=true;
                }
            }
            secondsSinceSave+=seconds;
            if(save || secondsSinceSave>=5) { Persist(); secondsSinceSave=0; }
        }
    }
    private void FinishBoss(CoopBossState boss)
    {
        if(boss.Rewarded) return;
        foreach(var session in sessions.Values.Where(x=>boss.Contributions.GetValueOrDefault(x.Account.Id)>0))
        {
            if(!session.Account.GrantedEncounters.Add(boss.Id)) continue;
            session.Game.FinishSharedBoss(boss.Region);
            if(session.Game.LastCatch is { } caught) Score(session.Account,caught);
        }
        foreach(var member in sessions.Values.Where(x=>boss.Contributions.ContainsKey(x.Account.Id) && boss.Contributions[x.Account.Id]==0))
            member.Game.AbortFishing("Boss dikalahkan sebelum seranganmu mengenai sasaran. Belum ada kontribusi.");
        boss.Rewarded=true; boss.CompletedAt=state.ElapsedSeconds;
    }
    private void Score(CoopAccount account,CatchRecord caught)
    {
        foreach(var cadence in new[]{"daily","weekly","seasonal"})
        {
            var (key,start,end)=TournamentPeriods.Get(cadence,clock.GetUtcNow());
            var score=state.Scores.FirstOrDefault(x=>x.PlayerId==account.Id && x.Period==key);
            if(score is null)
            { score=new() { Period=key,PlayerId=account.Id,Name=account.Name,StartsAt=start,EndsAt=end }; state.Scores.Add(score); }
            score.Score+=Math.Max(1,caught.Value);
            score.Catches++; score.LargestWeight=Math.Max(score.LargestWeight,caught.Weight);
        }
        state.Scores.RemoveAll(x=>x.EndsAt<clock.GetUtcNow().AddDays(-100));
    }
    public LeaderboardView Leaderboard(string cadence,bool previous=false)
    {
        lock(gate)
        {
            var (key,start,end)=TournamentPeriods.Get(cadence,clock.GetUtcNow(),previous);
            return Board(key,start,end);
        }
    }
    private LeaderboardView Board(string key,DateTimeOffset start,DateTimeOffset end)
    {
        var scores=state.Scores.Where(x=>x.Period==key).OrderByDescending(x=>x.Score).ThenByDescending(x=>x.LargestWeight).ThenBy(x=>x.PlayerId)
            .Select((x,i)=>new LeaderboardEntry(x.PlayerId,x.Name,x.Score,x.Catches,x.LargestWeight,i+1)).ToArray();
        return new(key,start,end,clock.GetUtcNow()>=end,scores);
    }
    private bool ClaimTournament(Session session,string cadence)
    {
        var (key,start,end)=TournamentPeriods.Get(cadence,clock.GetUtcNow(),true);
        var board=Board(key,start,end);
        var entry=board.Entries.FirstOrDefault(x=>x.PlayerId==session.Account.Id);
        if(!board.Finished || entry is null || !session.Account.ClaimedPeriods.Add(key)) return false;
        session.Game.Player.Coins+=entry.Rank switch { 1=>1000,2=>500,3=>250,_=>100 };
        if (entry.Rank <= 3)
        {
            session.Game.Player.Trophies.Add(new(key, entry.Rank, clock.GetUtcNow()));
            if (session.Game.Player.Trophies.Count > 256) session.Game.Player.Trophies.RemoveAt(0);
        }
        return true;
    }
    private RoomView View(Session session)
    {
        var account=session.Account;
        var peers=sessions.Values.Where(x=>x.Account.Room==account.Room && Active(x)).Select(x=>
        {
            var p=x.Game.Player;
            return new PeerView(x.Account.Id,x.Account.Name,p.CharacterId,p.RegionId,p.Level,p.BoatIndex,p.PositionX,p.PositionZ,p.BoatX,p.BoatZ,p.OnFoot,x.Game.Phase,p.InWater,p.Climbing,x.Game.JumpHeight,p.BoatHeading);
        }).ToArray();
        var boss=CurrentBoss(session);
        return new(account.Room,account.Mode,account.Id,session.Game.Snapshot(),peers,
            boss is null?null:new(boss.Id,boss.Region,boss.Health,boss.Maximum,boss.Health>0?BossCue(boss):boss.Expired?"Waktu encounter habis.":"Boss dikalahkan!",boss.Contributions.Count(x=>x.Value>0)),clock.GetUtcNow());
    }
    private string BossCue(CoopBossState boss)=>WaitingForParty(boss) ? BossRules.WaitingForParty : ((state.ElapsedSeconds-boss.StartedAt)%9) switch
    { <2=>"BERSIAP • Lepas tarikan saat serangan datang.",<4=>"SERANGAN • Lindungi tali.",_=>"Boss kelelahan • tarik bersama." };
    public void Flush() { lock(gate) Persist(); }
    private void Persist()
    {
        foreach(var session in sessions.Values) session.Account.Player=session.Game.Player.Copy();
        if(path is not null) CoopPersistence.Save(path,state);
    }
}
