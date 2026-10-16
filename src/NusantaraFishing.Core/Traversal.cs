using System.Numerics;

namespace NusantaraFishing.Core;

/// <summary>Half extents of a boat tier's rendered hull, outriggers included.</summary>
public readonly record struct BoatHull(float HalfLength, float HalfWidth)
{
    private static readonly BoatHull[] Tiers =
    [
        new(2f, 1.9f),    // jukung with outriggers
        new(1.65f, 1f),   // bamboo raft
        new(1.56f, 1.56f),// tire float
        new(2.1f, 1.3f),  // swan pedal boat
        new(2.8f, 1.2f),  // cabin cruiser
        new(2.5f, .8f)    // speedboat
    ];
    public static BoatHull For(int boatIndex) => Tiers[Math.Clamp(boatIndex, 0, Tiers.Length - 1)];
}

public static class Traversal
{
    /// <summary>Points the bow along a heading, using the same yaw convention as the renderer.</summary>
    public static Vector2 Forward(float heading) => new(-MathF.Sin(heading), -MathF.Cos(heading));
    public static Vector2 Position(PlayerState player) => new(player.PositionX, player.PositionZ);
    public static void SetPosition(PlayerState player, Vector2 point)
    {
        player.PositionX = point.X; player.PositionZ = point.Y;
        if (!player.OnFoot && !player.InWater) { player.BoatX = point.X; player.BoatZ = point.Y; }
    }

    public static void Move(PlayerState player, WorldDefinition world, float seconds, Vector2 input, bool boosted, bool airborne = false)
    {
        if (!float.IsFinite(seconds) || seconds <= 0 || !float.IsFinite(input.X) || !float.IsFinite(input.Y)) return;
        seconds = Math.Min(seconds, .1f);
        if (input.LengthSquared() > 1) input = Vector2.Normalize(input);
        if (player.Climbing) return;
        var speed = player.InWater ? 1.8f : player.OnFoot ? 3.2f : 3 + player.BoatIndex * .6f;
        if (boosted) speed *= 1.7f;
        var drift = player.OnFoot ? Vector2.Zero : world.Current * (world.Biome == Biome.River ? 1.5f : 1);
        var start = Position(player);
        var delta = (input * speed + drift) * seconds;
        Func<Vector2, bool> allowed = player.OnFoot ?
            point => (airborne && Math.Abs(point.X) < WorldDefinition.Boundary && Math.Abs(point.Y) < WorldDefinition.Boundary && !(world.HasGovernmentPier && WorldDefinition.GovernmentPier.Contains(point) && player.CharacterId != "tono")) || world.CanWalk(point, player.CharacterId is "ojan" or "pendaki", player.CharacterId == "tono") :
            player.InWater ? point => world.CanSail(point) : BoatRule(player, world, start, input);
        // Sliding along banks is possible; diagonal movement cannot tunnel through thin walls.
        var candidate = new Vector2(start.X + delta.X, start.Y);
        if (!allowed(candidate)) candidate.X = start.X;
        var zCandidate = candidate + new Vector2(0, delta.Y);
        if (allowed(zCandidate)) candidate = zCandidate;
        SetPosition(player, candidate);
    }

    private static Func<Vector2, bool> BoatRule(PlayerState player, WorldDefinition world, Vector2 start, Vector2 input)
    {
        var hull = BoatHull.For(player.BoatIndex);
        var current = Forward(player.BoatHeading);
        if (input.LengthSquared() > .0001f)
        {
            // The bow follows the helm unless turning would push it into a wall.
            var heading = MathF.Atan2(-input.X, -input.Y);
            if (world.HullFits(start, Forward(heading), hull) || !world.HullFits(start, current, hull))
            {
                player.BoatHeading = heading;
                current = Forward(heading);
            }
        }
        var forward = current;
        // Hulls left overlapping by older saves may motor out under the previous point rule.
        if (!world.HullFits(start, forward, hull)) return point => world.CanSail(point);
        return point => world.HullFits(point, forward, hull);
    }
}
