using System.Numerics;
using NusantaraFishing.Core;
using ThreeNet;

namespace NusantaraFishing.Desktop;

public sealed partial class IslandWorld
{
    private Node? catchVisual;
    private Node? catchMotion;
    private string? catchVisualId;
    private string? bossVisualId;
    private Material[] catchMaterials = [];
    private MaterialOptions[] catchMaterialDefaults = [];
    private readonly Dictionary<uint, (AnimationClip Clip, AnimationPlayer Player)[]> catchAnimations = [];
    private Node? camouflageRock;
    private Node? inkCloud;
    private Node? diveSpear;
    private (Node Node, Vector3 Position)[] catchMotifs = [];
    private readonly WakeTrail hookedWake = new(.3f, 1, .45f, .45f, .2f, 1.5f);

    private Node? CatchModel(string id, Node parent, float length)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets/models", "catch-" + id + ".glb");
        if (!File.Exists(path))
        {
            AssetWarning = "Missing catch model: " + id;
            return null;
        }
        var previous = Scene.Animations.Select(x => x.Id).ToHashSet();
        var model = Scene.LoadGltf(path, parent).Root;
        var clips = Scene.Animations.Where(x => !previous.Contains(x.Id)).ToArray();
        if (clips.Length > 0) catchAnimations[parent.Id] = clips.Select(x => (x, x.Play())).ToArray();
        // Bounds are world-space: normalize while the wrapper is still at identity.
        var bounds = Scene.GetBounds(model);
        var scale = length / Math.Max(.01f, Math.Max(bounds.Size.X, Math.Max(bounds.Size.Y, bounds.Size.Z)));
        model.Scale = new(scale);
        model.Position = -bounds.Center * scale;
        return model;
    }

    private void ReleaseCatch(Node root)
    {
        foreach (var node in Descendants(root))
            if (catchAnimations.Remove(node.Id, out var animations))
                foreach (var (clip, player) in animations) { player.Stop(); clip.Destroy(); }
        Scene.Remove(root);
    }

    private void UpdateCatchVisual(float time)
    {
        if (camouflageRock is not null) camouflageRock.Visible = false;
        if (inkCloud is not null) inkCloud.Visible = false;
        var ink = game.Busy && (game.BossCue.StartsWith("TINTA", StringComparison.Ordinal) || game.BossCue.Contains("tinta", StringComparison.Ordinal) && game.BossCue.Contains("SERANGAN", StringComparison.Ordinal));
        if (ink)
        {
            if (inkCloud is null)
            {
                var material = Scene.CreateMaterial(MaterialOptions.Pbr(new(.025f, .018f, .045f, .65f), 0, 1) with { AlphaMode = AlphaMode.Blend, DepthWrite = false });
                inkCloud = Scene.AddMesh(sphere, material, name: "octopus-ink");
                inkCloud.CastShadow = false;
            }
            inkCloud.Visible = true;
            inkCloud.Position = new(game.HookPosition.X, -.5f, game.HookPosition.Y);
            inkCloud.Scale = new(1.5f + MathF.Sin(time * 2) * .2f, .7f, 1.5f);
        }
        var species = game.Hooked;
        if (species is null || species.Kind == CatchKind.Boss)
        {
            if (catchVisual is not null) catchVisual.Visible = false;
            return;
        }
        if (species.Id != catchVisualId)
        {
            if (catchVisual is not null) ReleaseCatch(catchVisual);
            catchVisual = Scene.CreateNode(name: "hooked-" + species.Id);
            var model = CatchModel(species.Id, catchVisual, species.Id == "raja" ? 2.2f : 1.3f);
            catchMotion = model is null ? null : Descendants(model).FirstOrDefault(x => x.Name == "motion");
            catchMotifs = model is null ? [] : Descendants(model).Where(x => x.Name.StartsWith("batik-motif", StringComparison.Ordinal)).Select(x => (x, x.Position)).ToArray();
            // Each encounter owns its animated material; museum instances keep their appearance.
            var nodes = Descendants(catchVisual).Where(x => x.Geometry is not null && x.Material is not null).ToArray();
            foreach (var node in nodes) node.AttachMesh(node.Geometry!, Scene.CreateMaterial(node.Material!.Options));
            catchMaterials = nodes.Select(x => x.Material!).ToArray();
            catchMaterialDefaults = catchMaterials.Select(x => x.Options).ToArray();
            catchVisualId = species.Id;
        }
        var landed = game.Phase == FishingPhase.Landed;
        var visible = game.Phase is FishingPhase.Fighting or FishingPhase.Landed;
        catchVisual!.Visible = visible;
        if (!visible || landed) hookedWake.Reset();
        if (!visible) return;
        if (species.Id == "gurita" && game.BossCue.StartsWith("KAMUFLASE", StringComparison.Ordinal) && !landed)
        {
            camouflageRock ??= Asset("karst", Vector3.Zero, new(.8f), isStatic: false, smallRock: true);
            if (camouflageRock is not null)
            {
                camouflageRock.Visible = true;
                camouflageRock.Position = new(game.HookPosition.X, -.6f, game.HookPosition.Y);
                catchVisual.Visible = false;
                return;
            }
        }
        // Shadow Piranha remains invisible while fighting; its discovery is shown on landing.
        if (species.Id == "shadow" && !landed) { catchVisual.Visible = false; return; }
        if (species.Id == "phantom" && !landed) catchVisual.Visible = time % 3 < 2;
        var dart = species.Id == "roket" && !landed ? MathF.Sin(time * 8) * .9f : 0;
        catchVisual.Position = game.Player.InWater && game.Dive is { } dive
            ? new(dive.TargetX, GameSession.DiveDepth, dive.TargetZ)
            : landed
            ? new(game.Player.PositionX + 1.8f, 1.4f, game.Player.PositionZ - 1)
            : new(game.HookPosition.X + dart, -.35f + MathF.Sin(time * 3) * .12f, game.HookPosition.Y);
        catchVisual.EulerAngles = new(0, landed ? time * .35f : (float)game.FishDirection * .8f, 0);
        // A hooked fish thrashing just under the surface drags ripples behind the line.
        if (!landed && game.Player.CatchingMethod == CatchingMethod.Rod) hookedWake.Update(water, catchVisual.Position, lastDt);
        if (catchMotion is not null) catchMotion.EulerAngles = new(0, MathF.Sin(time * 6) * .12f, 0);
        for (var i = 0; i < catchMotifs.Length; i++)
        {
            var (node, original) = catchMotifs[i];
            node.Position = original + new Vector3(MathF.Sin(time + i) * .015f, MathF.Cos(time + i) * .015f, 0);
        }
        if (species.Id is "disco" or "batik" || game.Region.Id == "kelimutu")
        {
            var color = new Vector4(.45f + .4f * MathF.Sin(time * 2), .45f + .4f * MathF.Sin(time * 2 + 2.1f), .45f + .4f * MathF.Sin(time * 2 + 4.2f), 1);
            for (var i = 0; i < catchMaterials.Length; i++)
            {
                var original = catchMaterialDefaults[i];
                catchMaterials[i].Update(x => x with { BaseColor = Vector4.Lerp(original.BaseColor, color, .65f) });
            }
        }
    }

    private void UpdateDiveSpear()
    {
        if (game.Player.CatchingMethod != CatchingMethod.Spear)
        {
            if (diveSpear is not null) diveSpear.Visible = false;
            return;
        }
        if (diveSpear is null)
        {
            diveSpear = Scene.CreateNode(name: "underwater-spear");
            Mesh(cylinder, "#92734E", Vector3.Zero, new(.025f, 1.4f, .025f), diveSpear);
            Mesh(cone, "#D4DFDC", new(0, .85f, 0), new(.06f, .3f, .06f), diveSpear);
        }
        diveSpear.Visible = game.Dive?.Stage is not (DiveStage.Complete or DiveStage.Miss);
        var aim = game.DiveAim;
        var forward = new Vector3(aim.X, 0, aim.Y);
        diveSpear.Position = game.Dive is { Stage: DiveStage.Projectile } shot
            ? new(shot.ProjectileX, GameSession.DiveDepth, shot.ProjectileZ)
            : new Vector3(game.Player.PositionX, GameSession.DiveDepth - .18f, game.Player.PositionZ) +
                new Vector3(-aim.Y, 0, aim.X) * .28f + forward * (.85f - (game.Dive?.Charge ?? 0) * .25f);
        diveSpear.Rotation = Quaternion.CreateFromAxisAngle(Vector3.Cross(Vector3.UnitY, forward), MathF.PI / 2);
    }

    private void EnsureBossModel(string id)
    {
        if (bossVisualId == id) return;
        if (bossVisual is not null) ReleaseCatch(bossVisual);
        bossArms.Clear();
        bossVisual = Scene.CreateNode(name: "regional-boss-" + id);
        var length = id == "boss-kubangan" ? 12 : id is "boss-awan" or "paus-kubangan" ? 10 : 7;
        var model = CatchModel(id, bossVisual, length);
        if (model is not null) bossArms.AddRange(Descendants(model).Where(x => x.Name == "motion"));
        bossVisualId = id;
        bossVisual.Visible = false;
    }
}
