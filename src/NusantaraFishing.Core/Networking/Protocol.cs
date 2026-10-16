namespace NusantaraFishing.Core.Networking;

public static class CoopProtocol
{
    public const int Version=2;
    public const int CoopCapacity=4;
    public const int FestivalCapacity=32;
}
public enum RoomMode { Coop,Festival }
public sealed record JoinRequest(string Name,string Room="harbor",RoomMode Mode=RoomMode.Coop,int Protocol=CoopProtocol.Version,string? ResumeToken=null);
public sealed record JoinReply(string Token,string PlayerId,long LastSequence,RoomView View);
public sealed record PlayerCommand(long Sequence,string Kind,string? Target=null,int Slot=0,float X=0,float Z=0,bool Reel=false,float Direction=0);
public sealed record ActionRequest(string Kind,string? Target=null,int Slot=0);
public sealed record PeerView(string Id,string Name,string CharacterId,string RegionId,int Level,int BoatIndex,float X,float Z,float BoatX,float BoatZ,bool OnFoot,FishingPhase Phase,bool InWater=false,bool Climbing=false,float JumpHeight=0,float BoatHeading=0);
public sealed record BossView(string Id,string RegionId,double Health,double Maximum,string Cue,int Contributors);
public sealed record RoomView(string Room,RoomMode Mode,string PlayerId,SimulationSnapshot Self,PeerView[] Players,BossView? Boss,DateTimeOffset ServerTime);
public sealed record CommandReply(bool Accepted,string? Error,RoomView View);
public sealed record LeaderboardEntry(string PlayerId,string Name,long Score,int Catches,double LargestWeight,int Rank);
public sealed record LeaderboardView(string Period,DateTimeOffset StartsAt,DateTimeOffset EndsAt,bool Finished,LeaderboardEntry[] Entries);
public sealed class NetworkRuleException(string message,int statusCode=400) : Exception(message)
{ public int StatusCode { get; }=statusCode; }
