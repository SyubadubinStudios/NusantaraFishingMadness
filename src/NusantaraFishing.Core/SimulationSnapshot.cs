namespace NusantaraFishing.Core;

public sealed record SimulationSnapshot(PlayerState Player,FishingPhase Phase,Weather Weather,string? HookedId,CatchRecord? LastCatch,
    double Tension,double Progress,double FishDirection,string BossCue,string Message,double AbilitySeconds,float JumpHeight,DiveEncounter? Dive=null);
