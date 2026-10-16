using System.Numerics;
using ThreeNet;
using ThreeNet.Effects;

namespace NusantaraFishing.Desktop;

/// <summary>
/// Long-period swell for floating bodies. The GPU surface only shades fine detail, so this
/// field drives pitch/roll and a few centimetres of heave rather than visible displacement.
/// </summary>
internal static class Swell
{
    public static float Height(float x, float z, float time) =>
        MathF.Sin(x * .23f + z * .11f + time * 1.15f) * .55f +
        MathF.Sin(x * -.09f + z * .29f + time * .83f) * .3f +
        MathF.Sin(x * .41f - z * .37f + time * 1.9f) * .15f;
}

/// <summary>Emits ripples spaced by distance travelled, so wakes are independent of frame rate.</summary>
internal sealed class WakeTrail(float spacing, float radius, float strength, float halfLength = 0, float halfWidth = 0, float fullSpeed = 4)
{
    private Vector2 last;
    private bool started;
    private float travelled;
    private bool port;

    public float HalfLength { get; set; } = halfLength;
    public float HalfWidth { get; set; } = halfWidth;

    public void Reset() => started = false;

    public void Update(WaterSurface water, Vector3 position, float dt)
    {
        var point = new Vector2(position.X, position.Z);
        var step = point - last;
        var distance = step.Length();
        // Teleports (region change, boarding, respawn) restart the trail instead of drawing a streak.
        if (!started || distance > 6) { last = point; started = true; travelled = 0; return; }
        last = point;
        if (distance < 1e-4f || dt <= 0) return;
        travelled += distance;
        var direction = step / distance;
        var side = new Vector2(-direction.Y, direction.X);
        var intensity = Math.Clamp(distance / dt / fullSpeed, .3f, 1.2f);
        for (var emitted = 0; travelled >= spacing && emitted < 3; emitted++)
        {
            travelled -= spacing;
            // Bow and alternating stern quarters push the surface the same way, forming a V.
            // Opposite signs this close together cancel within a cell or two of the coarse field.
            var bow = point + direction * HalfLength;
            water.Splash(new(bow.X, 0, bow.Y), radius, strength * .6f * intensity);
            var stern = point - direction * HalfLength * .8f + side * (port ? HalfWidth : -HalfWidth);
            water.Splash(new(stern.X, 0, stern.Y), radius * 1.15f, strength * intensity);
            port = !port;
        }
        if (travelled > spacing) travelled = spacing;
    }
}

/// <summary>Smoothed buoyant response of a hull to swell and simulated ripples.</summary>
internal sealed class FloatingBody(float halfLength, float halfWidth)
{
    private float pitch, roll, heave;
    private bool settled;

    public float HalfLength { get; set; } = halfLength;
    public float HalfWidth { get; set; } = halfWidth;

    /// <param name="swell">Swell amplitude in metres.</param>
    /// <param name="trim">Extra bow-up pitch, e.g. from planing speed.</param>
    /// <param name="bank">Extra roll, e.g. leaning into a turn.</param>
    public void Apply(Node node, WaterSurface water, Vector3 rest, float yaw, float time, float dt, float swell, float trim = 0, float bank = 0)
    {
        var forward = new Vector2(-MathF.Sin(yaw), -MathF.Cos(yaw));
        var right = new Vector2(MathF.Cos(yaw), -MathF.Sin(yaw));
        var center = new Vector2(rest.X, rest.Z);
        float Ripple(Vector2 p) => water.HeightAt(p.X, p.Y) - water.Level;
        float Sample(Vector2 p) => Swell.Height(p.X, p.Y, time) * swell + Ripple(p);
        var bow = Sample(center + forward * HalfLength);
        var stern = Sample(center - forward * HalfLength);
        var starboard = Sample(center + right * HalfWidth);
        var portSide = Sample(center - right * HalfWidth);
        // Hulls also roll at their own short natural period, not only with the wave slope.
        var natural = MathF.Sin(time * 1.35f + rest.X * .3f) * .45f + MathF.Sin(time * .62f + rest.Z * .2f) * .25f;
        var targetPitch = MathF.Atan2(bow - stern, HalfLength * 2) + trim;
        var targetRoll = MathF.Atan2(starboard - portSide, HalfWidth * 2) + natural * swell + bank;
        // The rendered surface does not heave with the swell, so only a fraction of it lifts the
        // hull; simulated ripples are drawn and therefore followed in full.
        var targetHeave = Swell.Height(center.X, center.Y, time) * swell * .3f + Ripple(center);
        var follow = settled ? 1 - MathF.Exp(-dt * 5) : 1;
        settled = true;
        pitch += (targetPitch - pitch) * follow;
        roll += (targetRoll - roll) * follow;
        heave += (targetHeave - heave) * follow;
        node.Position = rest + new Vector3(0, heave, 0);
        node.Rotation = Quaternion.CreateFromYawPitchRoll(yaw, pitch, roll);
    }
}
