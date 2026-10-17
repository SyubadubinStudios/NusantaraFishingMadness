using System.Numerics;

namespace NusantaraFishing.Core;

public enum DiveStage { Approach, Aim, Projectile, Complete, Miss }

/// <summary>Server-owned encounter geometry in the horizontal swimming plane, 0.65 m below the surface.</summary>
public readonly record struct DiveEncounter(float OriginX, float OriginZ, float TargetX, float TargetZ,
    float AimYaw, float Charge, float Focus, float ProjectileX, float ProjectileZ, float Travelled,
    double Seconds, DiveStage Stage, bool TriggerHeld);

public sealed partial class GameSession
{
    public DiveEncounter? Dive { get; private set; }
    public const float DiveDepth = -.65f;
    public float DiveRange => Dive is { } dive ? Vector2.Distance(Traversal.Position(Player), new(dive.TargetX, dive.TargetZ)) : 0;
    public Vector2 DiveAim => Traversal.Forward(Dive?.AimYaw ?? 0);
    public bool DiveAligned => Dive is { } dive && DiveRange > .1f &&
        Vector2.Dot(Vector2.Normalize(new Vector2(dive.TargetX, dive.TargetZ) - Traversal.Position(Player)), DiveAim) > .99f;
    public bool DiveClear => Dive is { } dive && ClearDivePath(Traversal.Position(Player), new(dive.TargetX, dive.TargetZ));

    private bool ClearDivePath(Vector2 start, Vector2 end)
    {
        if (World.LineHitsReef(start, end)) return false;
        var steps = Math.Max(1, (int)MathF.Ceiling(Vector2.Distance(start, end) / .3f));
        for (var i = 1; i <= steps; i++)
            if (!World.CanSail(Vector2.Lerp(start, end, (float)i / steps), .1f)) return false;
        return true;
    }

    private bool BeginDive()
    {
        var origin = Traversal.Position(Player);
        // Search a reachable patch; never put the encounter inside an island or beyond a wall.
        for (var i = 0; i < 16; i++)
        {
            var yaw = MathF.IEEERemainder(i * MathF.Tau / 16, MathF.Tau);
            var target = origin + Traversal.Forward(yaw) * 8;
            if (!World.CanSail(target, 1) || !ClearDivePath(origin, target)) continue;
            Dive = new(target.X, target.Y, target.X, target.Y, yaw, 0, 0, origin.X, origin.Y, 0, 0, DiveStage.Approach, false);
            Hooked = RollSpecies();
            LastCatch = null;
            Phase = FishingPhase.Fighting;
            Progress = Tension = 0;
            BossCue = "Dekati target dengan berenang. Tahan aksi untuk membidik; kiri/kanan memutar bidikan.";
            Message = BossCue;
            return true;
        }
        Message = "Tidak ada target di air terbuka. Berenang menjauhi tepian dan coba lagi.";
        return false;
    }

    public bool CancelDive()
    {
        if (!Player.InWater || !Busy) return false;
        AbortFishing("Pengamatan dihentikan. Berenang kembali ke perahu atau cari target lain.");
        return true;
    }

    private void TickDive(double seconds, bool held, double turn)
    {
        if (Dive is not { } dive || Hooked is null || !Busy) return;
        var time = dive.Seconds + seconds;
        if (time >= 60 || DiveRange > 24)
        {
            Dive = dive with { Stage = DiveStage.Miss, TriggerHeld = false };
            Phase = FishingPhase.Escaped;
            Message = "Target menjauh. Cari satwa lain atau dekati lagi.";
            BossCue = "";
            return;
        }
        var target = new Vector2(dive.OriginX + MathF.Sin((float)time * .6f) * .7f, dive.OriginZ + MathF.Sin((float)time * .35f) * .35f);
        if (!World.CanSail(target, .6f)) target = new(dive.TargetX, dive.TargetZ);
        dive = dive with { Seconds = time, TargetX = target.X, TargetZ = target.Y };
        if (dive.Stage == DiveStage.Projectile)
        {
            var start = new Vector2(dive.ProjectileX, dive.ProjectileZ);
            var step = Math.Min(7 - dive.Travelled, (float)seconds * 14);
            var end = start + Traversal.Forward(dive.AimYaw) * step;
            dive = dive with { ProjectileX = end.X, ProjectileZ = end.Y, Travelled = dive.Travelled + step };
            Dive = dive;
            if (!ClearDivePath(start, end)) { MissDive("Tombak tertahan karang atau tepian. Cari jalur bidik terbuka."); return; }
            var segment = end - start;
            var closest = start + segment * Math.Clamp(Vector2.Dot(target - start, segment) / Math.Max(.0001f, segment.LengthSquared()), 0, 1);
            if (Vector2.Distance(closest, target) < .5f)
            {
                Dive = dive with { Stage = DiveStage.Complete, TriggerHeld = false };
                Progress = 1;
                BossCue = "";
                LandCatch();
            }
            else if (dive.Travelled >= 7) MissDive("Tombak meleset. Dekati target dan perbaiki bidikan sebelum melempar.");
            return;
        }
        if (held) dive = dive with { AimYaw = MathF.IEEERemainder(dive.AimYaw - (float)(turn * seconds * 1.2), MathF.Tau) };
        Dive = dive;
        var range = DiveRange;
        var clear = DiveClear;
        if (Player.CatchingMethod == CatchingMethod.Snorkel)
        {
            var focused = held && range is >= 1.2f and <= 4 && DiveAligned && clear;
            var focus = Math.Clamp(dive.Focus + (float)seconds * (focused ? .65f : -.9f), 0, 1);
            Dive = dive with { Stage = held ? DiveStage.Aim : DiveStage.Approach, Focus = focus, TriggerHeld = held };
            Progress = focus;
            BossCue = range < 1.2f ? "Terlalu dekat. Mundur agar satwa tetap tenang." : !clear ? "Bidikan terhalang. Berenang ke jalur terbuka." :
                range > 4 ? "Dekati satwa hingga 4 meter, lalu tahan aksi untuk dokumentasi." : "Tahan aksi dan pusatkan satwa di bidikan sampai fokus penuh.";
            if (focus >= 1)
            {
                Dive = Dive.Value with { Stage = DiveStage.Complete, TriggerHeld = false };
                BossCue = "";
                LandCatch();
            }
        }
        else
        {
            var charge = held ? Math.Min(1, dive.Charge + (float)seconds / .8f) : 0;
            Dive = dive with { Stage = held ? DiveStage.Aim : DiveStage.Approach, Charge = charge, TriggerHeld = held };
            Progress = charge;
            BossCue = !clear ? "Bidikan terhalang. Berenang ke jalur terbuka." : range > 6 ?
                "Dekati target hingga 6 meter. Tahan aksi untuk menyiapkan tombak." : "Tahan aksi untuk membidik; lepaskan setelah siap untuk melempar tombak.";
            if (!held && dive.TriggerHeld && dive.Charge >= .35f)
            {
                var start = Traversal.Position(Player) + DiveAim * .4f;
                Dive = dive with { Stage = DiveStage.Projectile, ProjectileX = start.X, ProjectileZ = start.Y, Travelled = 0, TriggerHeld = false };
                Progress = 0;
                BossCue = "Tombak melesat!";
            }
        }
        Tension = 0;
    }

    private void MissDive(string message)
    {
        Dive = Dive!.Value with { Stage = DiveStage.Miss, TriggerHeld = false };
        Phase = FishingPhase.Escaped;
        BossCue = "";
        Message = message;
    }
}
