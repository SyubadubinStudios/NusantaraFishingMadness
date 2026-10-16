using System.Numerics;
using NusantaraFishing.Core;
using ThreeNet;

namespace NusantaraFishing.Desktop;

public sealed partial class IslandWorld
{
    private sealed record AmbientActor(Node Root, string Kind, Vector3 Start, float Speed, float Phase, PersonVisual? Person = null, Node[]? Wings = null)
    {
        public FloatingBody? Float { get; init; }
        public WakeTrail? Wake { get; init; }
        public float PreviousY { get; set; }
    }
    private readonly List<AmbientActor> actors = [];
    public int AmbientActorCount => actors.Count;
    public int ResidentTerrainChunks => terrain.ResidentChunks;

    private void BuildLivingWorld()
    {
        var biome = game.World.Biome;
        if (game.World.HasGovernmentPier)
        {
            var guard = Scene.CreateNode(name: "government-guard");
            var visual = Person(guard, "#B4A27B", character: "satpam");
            actors.Add(new(guard, "guard", new(16, 1.1f, 29.6f), 0, 0, visual));
        }
        for (var i = 0; i < 5; i++)
        {
            var person = Scene.CreateNode(name: i == 0 ? "fisherman" : "resident");
            var visual = Person(person, i % 2 == 0 ? "#B98C58" : "#648F94", character: i == 1 ? "bakso" : i == 4 ? "satpam" : i == 3 ? "rina" : "ujang");
            var start = i == 0 ? new Vector3(-7, .65f, 1) : new(-23 + i * 3, .65f, i == 1 ? -6 : -4.3f);
            actors.Add(new(person, i == 0 ? "angler" : i == 1 ? "vendor" : "resident", start, .35f, i, visual));
        }
        // Visitors only appear after the player places a real exhibit.
        for (var i = 0; i < 3; i++)
        {
            var person = Scene.CreateNode(name: "exhibition-visitor");
            actors.Add(new(person, "visitor", new(-22 + i * 4, .65f, -4.3f), .3f, i, Person(person, "#B77B8E")));
        }
        if (!game.World.Underground)
        {
            for (var i = 0; i < 9; i++)
            {
                var bird = Scene.CreateNode(name: "seabird");
                if (Asset("seabird", Vector3.Zero, Vector3.One, bird, false) is { } gull)
                {
                    var wings = Descendants(gull).Where(n => n.Name.StartsWith("left-wing", StringComparison.Ordinal) || n.Name.StartsWith("right-wing", StringComparison.Ordinal)).OrderBy(n => n.Name).ToArray();
                    actors.Add(new(bird, "bird", new(0, 8 + i * .9f, 0), .18f + i * .01f, i, null, wings));
                    continue;
                }
                Mesh(sphere, "#D7DFD8", Vector3.Zero, new(.3f, .14f, .16f), bird);
                var left = Mesh(box, "#D3DBD4", new(-.5f, 0, 0), new(.9f, .045f, .25f), bird);
                var right = Mesh(box, "#D3DBD4", new(.5f, 0, 0), new(.9f, .045f, .25f), bird);
                actors.Add(new(bird, "bird", new(0, 8 + i * .9f, 0), .18f + i * .01f, i, null, [left, right]));
            }
            for (var i = 0; i < 3; i++)
            {
                var craft = Scene.CreateNode(name: i == 0 ? "passing-fishing-boat" : i == 1 ? "jetski" : "surfer");
                if (i == 0)
                {
                    if (Asset("jukung", Vector3.Zero, Vector3.One, craft, false) is { } hull)
                    {
                        var bounds = Scene.GetBounds(hull);
                        var scale = 4f / Math.Max(bounds.Size.X, Math.Max(bounds.Size.Y, bounds.Size.Z));
                        hull.Scale = new(scale);
                        hull.Position = new(-bounds.Center.X * scale, -bounds.Min.Y * scale - .3f, -bounds.Center.Z * scale);
                    }
                }
                else Asset(i == 1 ? "jetski" : "surfboard", Vector3.Zero, Vector3.One, craft, false);
                var rider = Scene.CreateNode(craft);
                rider.Position = new(0, .2f, 0);
                var visual = Person(rider, i == 1 ? "#D56D47" : "#7699B2");
                if (i == 2) rider.EulerAngles = new(0, .5f, .12f);
                actors.Add(new(craft, i == 0 ? "boat" : i == 1 ? "jetski" : "surfer", new(i == 0 ? 14 : i == 1 ? 24 : -16, 0, i == 0 ? -10 : 6), i == 1 ? 1.8f : .55f, i, visual)
                {
                    Float = i == 0 ? new(2, 1.9f) : i == 1 ? new(1.5f, .6f) : new(1.1f, .35f),
                    Wake = i == 0 ? new(.7f, 1.5f, .6f, 1.7f, 1.5f, 3) : i == 1 ? new(.45f, 1.3f, .75f, 1.3f, .5f, 9) : new(.55f, 1.1f, .4f, .9f, .3f, 3)
                });
            }
        }
        if (biome == Biome.City)
        {
            var drifter = Scene.CreateNode(name: "resident-on-life-ring");
            var tire = Scene.CreateTorusGeometry(.7f, .15f, 8, 16);
            Mesh(tire, "#D2A153", Vector3.Zero, Vector3.One, drifter).EulerAngles = new(MathF.PI / 2, 0, 0);
            var person = Scene.CreateNode(drifter); person.Position = new(0, -.4f, 0);
            actors.Add(new(drifter, "drifter", new(12, 0, 7), .2f, 0, Person(person, "#7F97AB")) { Float = new(.85f, .85f), Wake = new(.5f, 1.1f, .28f, .7f, .7f, 1) });
        }
        if (biome is Biome.River or Biome.Lake)
        {
            var crocodile = Scene.CreateNode(name: "wild-crocodile");
            CatchModel("buaya", crocodile, 4);
            actors.Add(new(crocodile, "crocodile", new(13, -.1f, 14), .2f, 2) { Wake = new(.55f, 1.2f, .45f, 1.8f, .4f, 2) });
            var wildlife = Scene.CreateNode(name: "riverside-orangutan");
            actors.Add(new(wildlife, "wildlife", new(-28, 3, -12), .2f, 0, Person(wildlife, "#8B5837", "#A8734C", "orangutan")));
        }
        if (biome is Biome.Coast or Biome.Reef or Biome.Sky)
        {
            for (var i = 0; i < 3; i++)
            {
                var dolphin = Scene.CreateNode(name: "wild-dolphin");
                if (Asset("dolphin", Vector3.Zero, Vector3.One, dolphin, false) is { } detailedDolphin)
                {
                    actors.Add(new(dolphin, "dolphin", new(14, -.7f, -5 + i * 2), .5f, i, null,
                        Descendants(detailedDolphin).Where(n => n.Name.StartsWith("dolphin-tail", StringComparison.Ordinal)).ToArray()) { Wake = new(.55f, 1.2f, .5f, .9f, .3f, 5) });
                    continue;
                }
                Mesh(sphere, "#6E9DAB", Vector3.Zero, new(.35f, .35f, 1.1f), dolphin);
                Mesh(cone, "#7EAEB5", new(0, .4f, 0), new(.12f, .6f, .5f), dolphin);
                actors.Add(new(dolphin, "dolphin", new(14, -.7f, -5 + i * 2), .5f, i) { Wake = new(.55f, 1.2f, .5f, .9f, .3f, 5) });
            }
        }
        if (game.World.Underground)
        {
            for (var i = 0; i < 5; i++)
            {
                var bat = Scene.CreateNode(name: "cave-bat");
                Mesh(sphere, "#555479", Vector3.Zero, new(.18f, .2f, .12f), bat);
                var l = Mesh(cone, "#6A6488", new(-.4f, 0, 0), new(.5f, .1f, .3f), bat);
                var r = Mesh(cone, "#6A6488", new(.4f, 0, 0), new(.5f, .1f, .3f), bat);
                actors.Add(new(bat, "bat", new(0, 8 + i, 0), .3f, i, null, [l, r]));
            }
        }
    }

    private void UpdateLivingWorld(float dt, float time)
    {
        var daylight = game.Player.WorldHours is >= 6 and < 19;
        foreach (var actor in actors)
        {
            var t = time * actor.Speed + actor.Phase;
            var position = actor.Start;
            var angle = 0f;
            var active = true;
            switch (actor.Kind)
            {
                case "guard":
                    active = true;
                    if (actor.Person is { } guard) Walk(guard, time, 0);
                    break;
                case "bird":
                case "bat":
                    active = actor.Kind == "bat" || daylight;
                    position += new Vector3(MathF.Cos(t) * 24, MathF.Sin(t * 2) * 1.2f, MathF.Sin(t) * 17);
                    angle = -t;
                    if (actor.Wings is { Length: 2 })
                    {
                        actor.Wings[0].EulerAngles = new(0, 0, MathF.Sin(time * 9 + actor.Phase) * .6f);
                        actor.Wings[1].EulerAngles = new(0, 0, -MathF.Sin(time * 9 + actor.Phase) * .6f);
                    }
                    break;
                case "visitor":
                    active = daylight && game.Player.ExhibitSlots.Count > 0;
                    // Keep visitors in the front aisle, clear of tanks and display pedestals.
                    position += new Vector3(MathF.Sin(t) * .65f, 0, MathF.Cos(t) * .15f);
                    angle = -t;
                    if (actor.Person is { } visitor) Walk(visitor, time, 1);
                    break;
                case "resident":
                    active = daylight;
                    position += new Vector3(MathF.Sin(t) * 1.2f, 0, MathF.Cos(t) * .15f);
                    angle = -t;
                    if (actor.Person is { } walking) Walk(walking, time, 1);
                    break;
                case "angler":
                    active = daylight;
                    if (actor.Person is { } anglerNpc)
                    {
                        if (anglerNpc.Animation is { } animation) animation.Pose(0, true);
                        else anglerNpc.RightArm.EulerAngles = new(-.8f + MathF.Sin(t) * .2f, 0, 0);
                    }
                    break;
                case "vendor":
                    active = daylight;
                    if (actor.Person is { } vendor) vendor.LeftArm.EulerAngles = new(0, 0, .4f + MathF.Sin(t) * .2f);
                    break;
                case "wildlife":
                    active = daylight;
                    position += new Vector3(0, MathF.Sin(t) * .4f, 0);
                    if (actor.Person is { } ape) Walk(ape, time, .3f);
                    break;
                case "dolphin":
                    position += new Vector3(MathF.Cos(t) * 8, Math.Max(-.7f, MathF.Sin(t * 2) * 1.6f), MathF.Sin(t) * 5);
                    angle = -t;
                    if (actor.Wings is { Length: > 0 }) actor.Wings[0].EulerAngles = new(MathF.Sin(time * 4 + actor.Phase) * .28f, 0, 0);
                    break;
                default:
                    active = daylight || actor.Kind is "boat" or "drifter";
                    position += new Vector3(MathF.Sin(t * .25f) * 8, MathF.Sin(time * 1.3f + actor.Phase) * .08f, MathF.Cos(t * .25f) * 4);
                    angle = -t * .25f;
                    if (actor.Kind == "drifter") position += new Vector3(MathF.Sin(time * .04f) * 3, 0, 0);
                    break;
            }
            // Craft routes yield rather than passing through authored banks/buildings.
            if (actor.Kind is "boat" or "jetski" or "surfer" or "crocodile" && !game.World.CanSail(new(position.X, position.Z), 1)) active = false;
            actor.Root.Visible = active;
            if (actor.Float is { } body && active)
                body.Apply(actor.Root, water, position, angle, time, dt, SwellAmplitude(), actor.Kind == "jetski" ? .05f : 0, actor.Kind == "surfer" ? MathF.Sin(t) * .12f : 0);
            else
            {
                actor.Root.Position = position;
                actor.Root.EulerAngles = new(0, angle, actor.Kind == "surfer" ? MathF.Sin(t) * .12f : 0);
            }
            if (actor.Wake is { } wake)
            {
                // Only bodies breaking the surface draw a wake; breaching dolphins splash on entry and exit.
                var surfaced = active && position.Y > -.45f;
                if (surfaced) wake.Update(water, position, dt); else wake.Reset();
                if (active && actor.Kind == "dolphin" && MathF.Sign(position.Y + .05f) != MathF.Sign(actor.PreviousY + .05f))
                    water.Splash(position with { Y = 0 }, 1.5f, .25f);
                actor.PreviousY = position.Y;
            }
        }
    }
}
