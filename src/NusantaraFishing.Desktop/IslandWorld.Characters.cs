using System.Numerics;
using NusantaraFishing.Core;
using ThreeNet;

namespace NusantaraFishing.Desktop;

public sealed partial class IslandWorld
{
    private readonly record struct PersonVisual(Node Root, Node LeftArm, Node RightArm, Node LeftLeg, Node RightLeg, HumanAnimation? Animation = null);
    private PersonVisual playerVisual;
    private bool playerWasOnFoot;
    private bool playerWasInWater;
    private Node? playerHand;
    private Node? playerRod;
    private Node? playerDiveGear;
    private Node? spearTip;
    private readonly List<Node> bossArms = [];
    private readonly WakeTrail bossWake = new(.9f, 2, .45f, 3, 1.5f, 3);
    private bool bossSurfaced;

    private PersonVisual Person(Node parent, string outfit, string skin = "#BD8059", string type = "human", bool? female = null, string? character = null)
    {
        var isFemale = female ?? ++residentModelIndex % 3 == 0;
        var identity = character ?? (parent == angler ? game.Player.CharacterId : type == "orangutan" ? "ojan" : isFemale ? "rina" : "ujang");
        if (DetailedPerson(parent, identity) is { } detailed) return detailed;
        Mesh(cylinder, outfit, new(0, .95f, 0), new(.55f, .7f, .4f), parent);
        Mesh(sphere, skin, new(0, 1.53f, 0), new(.24f), parent);
        var leftArm = Mesh(cylinder, outfit, new(-.38f, .92f, 0), new(.15f, .8f, .15f), parent);
        var rightArm = Mesh(cylinder, outfit, new(.38f, .92f, 0), new(.15f, .8f, .15f), parent);
        var leftLeg = Mesh(cylinder, "#35515D", new(-.16f, .35f, 0), new(.18f, .7f, .2f), parent);
        var rightLeg = Mesh(cylinder, "#35515D", new(.16f, .35f, 0), new(.18f, .7f, .2f), parent);
        foreach (var x in new[] { -.075f, .075f }) Mesh(sphere, "#172B34", new(x, 1.57f, -.215f), new(.025f), parent);
        if (type == "orangutan")
        {
            leftArm.Scale = new(.23f, 1.1f, .23f); rightArm.Scale = leftArm.Scale;
            Mesh(sphere, "#6B402C", new(0, 1.55f, .07f), new(.34f), parent);
        }
        else if (type == "cat")
        {
            foreach (var x in new[] { -.18f, .18f }) Mesh(cone, "#D28A3E", new(x, 1.83f, 0), new(.18f, .3f, .18f), parent);
            Mesh(cylinder, "#D28A3E", new(0, .6f, .45f), new(.12f, .8f, .12f), parent).EulerAngles = new(.8f, 0, 0);
        }
        else if (type == "robot")
        {
            Mesh(box, "#B4CED2", new(0, 1.5f, 0), new(.5f, .5f, .4f), parent);
            Mesh(box, "#4BC6E1", new(0, 1.54f, -.23f), new(.35f, .08f, .04f), parent);
        }
        else Mesh(cone, "#D6BD7A", new(0, 1.82f, 0), new(.62f, .17f, .62f), parent);
        return new(parent, leftArm, rightArm, leftLeg, rightLeg);
    }

    private void BuildPlayer()
    {
        playerVisual.Animation?.Stop();
        if (angler is not null) Scene.Remove(angler);
        angler = Scene.CreateNode(Boat, name: "player-character");
        var id = game.Player.CharacterId;
        var color = id switch
        {
            "ujang" => "#EDAE55",
            "rina" => "#64AA7D",
            "bambang" => "#D8E4E1",
            "tono" => "#6B8EB6",
            "ojan" => "#8D573B",
            "ojol" => "#3DAD69",
            "satpam" => "#637281",
            "bakso" => "#C36A50",
            "kucing" => "#DB9341",
            "robot" => "#89ACB8",
            _ => "#BC7651"
        };
        playerVisual = Person(angler, color, id == "ojan" ? "#A66D4B" : id == "kucing" ? "#D28A3E" : "#BD8059",
            id == "ojan" ? "orangutan" : id == "kucing" ? "cat" : id == "robot" ? "robot" : "human");
        playerRod = Mesh(cylinder, "#766449", new(.3f, 1.25f, -1), new(.024f, 2.4f, .024f), angler);
        playerHand = Descendants(angler).FirstOrDefault(x => x.Name == "RightHand");
        playerDiveGear = Scene.CreateNode(angler, name: "snorkeling-equipment");
        Mesh(box, "#314E57", new(0, 1.59f, -.22f), new(.35f, .14f, .08f), playerDiveGear);
        Mesh(cylinder, "#D8AA45", new(.25f, 1.73f, -.12f), new(.035f, .45f, .035f), playerDiveGear);
        foreach (var sign in new[] { -1, 1 }) Mesh(box, "#248F96", new(sign * .15f, .04f, -.24f), new(.22f, .06f, .5f), playerDiveGear);
        spearTip = Mesh(cone, "#D4DFDC", Vector3.Zero, new(.055f, .3f, .055f), angler);
        characterVisualId = id;
        playerWasOnFoot = !game.Player.OnFoot;
    }

    private static void Walk(PersonVisual person, float time, float intensity)
    {
        if (person.Animation is { } animation) { animation.Pose(intensity); return; }
        var swing = MathF.Sin(time * 7) * .55f * intensity;
        person.LeftArm.EulerAngles = new(swing, 0, .1f);
        person.RightArm.EulerAngles = new(-swing, 0, -.1f);
        person.LeftLeg.EulerAngles = new(-swing, 0, 0);
        person.RightLeg.EulerAngles = new(swing, 0, 0);
    }

    private void BuildBoat()
    {
        if (boatHull is not null) Scene.Remove(boatHull);
        boatHull = Scene.CreateNode(Boat, name: "boat-hull");
        boatVisualIndex = game.Player.BoatIndex;
        if (boatVisualIndex == 0)
        {
            var path = Path.Combine(AppContext.BaseDirectory, "assets/models/jukung.glb");
            if (File.Exists(path))
            {
                try
                {
                    var model = Scene.LoadModelCached(path, boatHull);
                    ReduceGeometry(model.Root, .4f);
                    var bounds = Scene.GetBounds(model.Root);
                    var scale = 4f / Math.Max(.01f, Math.Max(bounds.Size.X, Math.Max(bounds.Size.Y, bounds.Size.Z)));
                    model.Root.Scale = new(scale);
                    model.Root.Position = new(-bounds.Center.X * scale, -bounds.Min.Y * scale - .3f, -bounds.Center.Z * scale);
                    return;
                }
                catch (Exception ex) when (ex is ThreeNetException or IOException) { AssetWarning = ex.Message; }
            }
        }
        switch (boatVisualIndex)
        {
            case 1:
                for (var i = 0; i < 7; i++) Mesh(cylinder, "#B4AE61", new((i - 3) * .27f, -.05f, 0), new(.28f, 3.3f, .28f), boatHull).EulerAngles = new(MathF.PI / 2, 0, 0);
                foreach (var z in new[] { -1f, 1f }) Mesh(box, "#7D7348", new(0, .13f, z), new(2, .12f, .12f), boatHull);
                break;
            case 2:
                var tire = Scene.CreateTorusGeometry(1, .3f, 12, 24);
                Mesh(tire, "#2F3437", new(0, 0, 0), new(1.2f, 1.2f, 1.2f), boatHull).EulerAngles = new(MathF.PI / 2, 0, 0);
                Mesh(box, "#A2855F", new(0, .18f, 0), new(1.7f, .12f, 1), boatHull);
                break;
            case 3:
                Mesh(sphere, "#D9BB4C", new(0, 0, 0), new(1.3f, .5f, 1.8f), boatHull);
                Mesh(cylinder, "#E1C458", new(0, .5f, -1.4f), new(.5f, 1.1f, .5f), boatHull);
                Mesh(sphere, "#E7CB5E", new(0, 1.2f, -1.5f), new(.5f), boatHull);
                Mesh(cone, "#DD8F39", new(0, 1.15f, -2.1f), new(.35f, .7f, .2f), boatHull).EulerAngles = new(MathF.PI / 2, 0, 0);
                break;
            case 4:
                Mesh(sphere, "#487E9B", new(0, 0, 0), new(1.2f, .5f, 2.8f), boatHull);
                Mesh(box, "#DBD8B4", new(0, .8f, .8f), new(1.5f, 1.3f, 1.5f), boatHull);
                Mesh(box, "#B96448", new(0, 1.6f, .8f), new(2, .18f, 1.9f), boatHull);
                Mesh(box, "#5A8B9B", new(0, 1, -.01f), new(1.1f, .5f, .04f), boatHull);
                break;
            default:
                Mesh(sphere, boatVisualIndex == 5 ? "#D9E0D8" : "#AA774C", new(0, 0, 0), new(1, .4f, 2.5f), boatHull);
                Mesh(box, boatVisualIndex == 5 ? "#BF5C44" : "#357F81", new(0, .26f, 0), new(1.55f, .16f, 3.3f), boatHull);
                if (boatVisualIndex == 5) Mesh(box, "#87B6C2", new(0, .8f, -.9f), new(1.5f, .7f, .12f), boatHull).EulerAngles = new(-.3f, 0, 0);
                break;
        }
    }

    private void UpdatePlayerVisual(Vector2 move, float time)
    {
        if (boatVisualIndex != game.Player.BoatIndex) BuildBoat();
        if (characterVisualId != game.Player.CharacterId) BuildPlayer();
        if (playerWasOnFoot != game.Player.OnFoot || playerWasInWater != game.Player.InWater)
        {
            angler.Parent = game.Player.OnFoot || game.Player.InWater ? null : Boat;
            playerWasOnFoot = game.Player.OnFoot;
            playerWasInWater = game.Player.InWater;
            angler.EulerAngles = Vector3.Zero;
        }
        // The underwater camera is first person; avoid looking through the local head/gear.
        // Remote swimmers and explicit review cameras still show their complete model.
        angler.Visible = !game.Player.InWater || reviewTarget is not null;
        angler.Position = game.Player.InWater ? new(game.Player.PositionX, -.65f, game.Player.PositionZ) : game.Player.OnFoot ? new(game.Player.PositionX, game.World.GroundHeight(Traversal.Position(game.Player)) + game.JumpHeight + (game.Player.Climbing ? 3 : 0), game.Player.PositionZ) : new(0, .3f, 0);
        if (game.Player.InWater) angler.EulerAngles = new(-MathF.PI / 2, 0, 0);
        if (game.Player.OnFoot && move.LengthSquared() > .01f) angler.EulerAngles = new(0, MathF.Atan2(-move.X, -move.Y), 0);
        if (playerVisual.Animation is { } animation)
            animation.Pose(game.Player.OnFoot && move.LengthSquared() > .01f ? 1 : 0, !game.Player.OnFoot || game.Busy);
        else
        {
            Walk(playerVisual, time, game.Player.OnFoot && move.LengthSquared() > .01f ? 1 : .08f);
            if (game.Busy) playerVisual.RightArm.EulerAngles = new(-.7f + (float)game.Tension * .5f, 0, -.3f);
        }
        if (playerRod is not null)
        {
            playerRod.Visible = game.Player.CatchingMethod is CatchingMethod.Rod or CatchingMethod.Spear;
            playerDiveGear!.Visible = game.Player.InWater;
            spearTip!.Visible = game.Player.CatchingMethod == CatchingMethod.Spear;
            Matrix4x4.Invert(angler.WorldMatrix, out var inverse);
            var hand = playerHand is null ? new Vector3(.3f, 1.1f, -.3f) : Vector3.Transform(playerHand.WorldPosition, inverse);
            var direction = Vector3.Normalize(new Vector3(.1f, .65f + (float)game.Tension * .3f, -1));
            playerRod.Position = hand + direction * 1.1f;
            playerRod.Rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY, direction)), MathF.Acos(direction.Y));
            spearTip.Position = hand + direction * 2.4f;
            spearTip.Rotation = playerRod.Rotation;
        }
    }

    private void BuildBoss() => EnsureBossModel("boss-" + game.Region.Id);

    private void UpdateBoss(float time)
    {
        var active = sharedBoss is { Health:>0 } || game.Hooked?.Kind == CatchKind.Boss && game.Phase == FishingPhase.Fighting;
        if (active) EnsureBossModel(sharedBoss is { Health: > 0 } ? "boss-" + game.Region.Id : game.Hooked!.Id);
        bossVisual.Visible = active;
        if (!active) { bossWake.Reset(); bossSurfaced = false; return; }
        bossVisual.Position = sharedBoss is { Health:>0 }?new(0,-.2f,-12):new(game.HookPosition.X, game.BossCue.Contains("SERANGAN", StringComparison.Ordinal) ? .7f : -.8f, game.HookPosition.Y - 4);
        bossVisual.EulerAngles = new(0, MathF.Sin(time * .6f) * .3f, 0);
        // Surfacing for an attack throws up a heavy splash; the boss's bulk drags a broad wake.
        var surfaced = bossVisual.Position.Y > 0;
        if (surfaced != bossSurfaced) water.Splash(bossVisual.Position with { Y = 0 }, 3, .3f);
        bossSurfaced = surfaced;
        bossWake.Update(water, bossVisual.Position, lastDt);
        for (var i = 0; i < bossArms.Count; i++) bossArms[i].Rotation = Quaternion.CreateFromYawPitchRoll(MathF.Sin(time * 2 + i) * .12f, MathF.Sin(time * 2 + i) * .2f, MathF.Cos(time * 2 + i) * .3f);
    }
}
