using System.Numerics;
using NusantaraFishing.Core;
using ThreeNet;

namespace NusantaraFishing.Desktop;

public sealed partial class IslandWorld
{
    private readonly List<(Node Node, Vector3 Start)> clouds = [];
    private readonly List<Node> nightLights = [];
    private readonly List<Node> vents = [];

    private void BuildLandmarks()
    {
        if (game.World.HasGovernmentPier)
        {
            var pier = WorldDefinition.GovernmentPier;
            Asset("government-outpost", new(pier.X, 0, pier.Z), Vector3.One);
        }
        foreach (var feature in game.World.Landmarks)
        {
            var pos = new Vector3(feature.X, feature.Height / 2, feature.Z);
            var scale = new Vector3(feature.Width, feature.Height, feature.Depth);
            switch (feature.Kind)
            {
                case LandmarkKind.Island:
                    IslandTerrain(feature);
                    for (var rockIndex = 0; rockIndex < 6; rockIndex++)
                    {
                        var a = rockIndex * MathF.Tau / 6 + feature.X * .13f;
                        var rock = Asset("karst-weathered", new(feature.X + MathF.Cos(a) * feature.Width * .39f, -.2f, feature.Z + MathF.Sin(a) * feature.Depth * .39f),
                            new(feature.Width * .3f, feature.Height * (.22f + .1f * (rockIndex % 3)), feature.Depth * .3f));
                        if (rock is not null) rock.EulerAngles = new(0, a, 0);
                        WeatheredRock(rock);
                    }
                    // Core's climbable trees stand on the crown; scattered vegetation fills the rest.
                    if (game.World.Biome is Biome.Coast or Biome.Reef or Biome.Lake)
                        for (var i = 0; i < 3; i++) Palm(new(feature.X + i * 2 - 2, IslandHeight(feature, feature.X + i * 2 - 2, feature.Z, out _), feature.Z), 1);
                    IslandVegetation(feature);
                    break;
                case LandmarkKind.Bank:
                    ShoreBank(new(feature.X, 0, feature.Z), scale, feature.Color);
                    for (var i = 0; i < 11; i++)
                    {
                        var tree = new Vector3(feature.X - feature.Width * .4f + i * feature.Width * .08f, feature.Height, feature.Z + MathF.Sin(i * 2.1f) * feature.Depth * .22f);
                        if (game.World.Biome == Biome.River) RainforestTree(tree, i);
                        else Palm(tree, 1.1f + i % 3 * .22f);
                        if (i % 2 == 0) Palm(tree + new Vector3(1.7f, 0, 1.5f), .3f);
                    }
                    BankVegetation(feature);
                    break;
                case LandmarkKind.Building:
                    if (game.World.Biome == Biome.River)
                        Asset("raft-house", new(feature.X, 0, feature.Z), new(feature.Width / 8, feature.Height / 5, feature.Depth / 6));
                    else Building(new(feature.X, 0, feature.Z), scale, feature.Color, game.World.Biome == Biome.City);
                    break;
                case LandmarkKind.Road:
                    Mesh(box, feature.Color, new(feature.X, .15f, feature.Z), scale);
                    for (var x = -3; x <= 3; x++) Mesh(box, "#E5D8A3", new(feature.X + x * 10, .7f, feature.Z), new(4, .025f, .22f));
                    break;
                case LandmarkKind.Bridge:
                    Mesh(box, feature.Color, new(feature.X, feature.Height, feature.Z), new(feature.Width, .6f, feature.Depth));
                    foreach (var sign in new[] { -1, 1 })
                        Mesh(box, "#B4BEB7", new(feature.X, feature.Height + .8f, feature.Z + sign * feature.Depth * .45f), new(feature.Width, .8f, .15f));
                    break;
                case LandmarkKind.Cliff:
                    // A recessed rock mass closes gaps between the individual eroded outcrops.
                    WeatheredRock(Mesh(box, feature.Color, new(feature.X, feature.Height * .29f, feature.Z),
                        new(feature.Width * .9f, feature.Height * .58f, feature.Depth * .78f)));
                    var count = Math.Clamp((int)(Math.Max(feature.Width, feature.Depth) / 8), 3, 10);
                    for (var i = 0; i < count; i++)
                    {
                        var longX = feature.Width >= feature.Depth;
                        var offset = (i / (float)(count - 1) - .5f) * .72f;
                        var rock = Asset("karst-weathered", new(feature.X + (longX ? offset * feature.Width : 0), -.1f, feature.Z + (longX ? 0 : offset * feature.Depth)),
                            new(longX ? feature.Width * 1.65f / count : feature.Width, feature.Height * (.85f + .2f * MathF.Sin(i * 1.7f)), longX ? feature.Depth : feature.Depth * 1.65f / count));
                        if (rock is not null) rock.EulerAngles = new(0, MathF.Sin(i * 2) * .15f, 0);
                        WeatheredRock(rock);
                    }
                    break;
                case LandmarkKind.Arch:
                    foreach (var sign in new[] { -1, 1 })
                        Mesh(cylinder, feature.Color, new(feature.X + sign * feature.Width * .43f, feature.Height * .45f, feature.Z), new(feature.Width * .15f, feature.Height * .9f, feature.Depth));
                    Mesh(box, feature.Color, new(feature.X, feature.Height, feature.Z), new(feature.Width, feature.Height * .18f, feature.Depth));
                    break;
                case LandmarkKind.Crystal:
                    var crystal = Mesh(cone, feature.Color, pos, scale);
                    crystal.EulerAngles = new(.12f, 0, .15f);
                    var light = Scene.AddLight(Light.Point(new(.35f, .6f, 1), 18, 20));
                    light.Position = pos + new Vector3(0, 2, 2);
                    break;
                case LandmarkKind.Waterfall:
                    var face = new Vector3(feature.X, feature.Height, feature.Z - .5f);
                    AddCascade([face, face with { Y = feature.Height * .72f, Z = feature.Z },
                        face with { Y = feature.Height * .3f, Z = feature.Z + .3f },
                        face with { Y = -.1f, Z = feature.Z + .7f }], feature.Width, Vector3.UnitZ);
                    break;
                case LandmarkKind.Vent:
                    Mesh(cone, feature.Color, new(feature.X, .2f, feature.Z), new(feature.Width, .8f, feature.Depth));
                    var plume = Mesh(sphere, feature.Color, new(feature.X, feature.Height, feature.Z), new(feature.Width * .35f, feature.Height, feature.Depth * .35f));
                    vents.Add(plume);
                    break;
                case LandmarkKind.Cloud:
                    Cloud(new(feature.X, 10, feature.Z), new(feature.Width * .5f, feature.Height * .5f, feature.Depth * .5f));
                    break;
                case LandmarkKind.Reef:
                    for (var i = 0; i < 5; i++)
                    {
                        var px = feature.X + (i % 3 - 1) * feature.Width * .25f;
                        var pz = feature.Z + (i / 3 - .5f) * feature.Depth * .3f;
                        Mesh(sphere, feature.Color, new(px, -game.World.DepthAt(px, pz) + .6f, pz), new(feature.Width * .23f, .8f, feature.Depth * .2f));
                    }
                    break;
                case LandmarkKind.Sign:
                    Mesh(cylinder, "#646B6A", pos, new(.18f, feature.Height, .18f));
                    Mesh(box, feature.Color, pos + new Vector3(0, feature.Height * .4f, 0), new(feature.Width, 1.2f, .2f));
                    break;
            }
        }
        if (game.World.Underground)
        {
            Mesh(box, "#3B4058", new(0, 27, 0), new(160, 4, 160));
            Mesh(box, "#484B63", new(0, 10, -72), new(160, 30, 5));
        }
        // The animated procedural sky supplies soft clouds without opaque ellipsoid silhouettes.
        // Harbor lamps make activities and boarding readable after sunset.
        foreach (var point in new[] { new Vector3(-7, 4, 1), new Vector3(-16, 4, -6), new Vector3(-23, 4, -10) })
        {
            Mesh(cylinder, "#655A47", point - new Vector3(0, 1.7f, 0), new(.12f, 3.4f, .12f));
            Mesh(sphere, "#F4D48B", point, new(.22f));
            var lamp = Scene.AddLight(Light.Point(new(1, .65f, .25f), 35, 13));
            lamp.Position = point; nightLights.Add(lamp);
        }
    }

    private void Building(Vector3 position, Vector3 size, string color, bool city = false)
    {
        Mesh(box, color, position + new Vector3(0, size.Y / 2, 0), size);
        if (!city) Mesh(cone, "#845342", position + new Vector3(0, size.Y + .8f, 0), new(size.X * .85f, 1.8f, size.Z * .85f));
        for (var floor = 0; floor < Math.Max(1, (int)size.Y / 3); floor++)
            for (var column = -1; column <= 1; column++)
                Mesh(box, "#C5D6C8", position + new Vector3(column * size.X * .25f, 1.6f + floor * 3, size.Z * .505f), new(.8f, 1, .04f));
        Mesh(box, "#365B61", position + new Vector3(0, 1, size.Z * .51f), new(1.2f, 2, .06f));
    }

    private void Cloud(Vector3 position, Vector3 scale)
    {
        var root = Scene.CreateNode(name: "moving-cloud"); root.Position = position;
        for (var i = 0; i < 3; i++) Mesh(sphere, "#D7E4E4", new(i * scale.X * .5f, 0, 0), scale, root).CastShadow = false;
        clouds.Add((root, position));
    }

    private void UpdateAtmosphere(float dt, float time)
    {
        var angle = ((float)game.Player.WorldHours - 6) / 12 * MathF.PI;
        var elevation = MathF.Sin(angle);
        var daylight = Math.Clamp(elevation * 1.25f, 0, 1);
        var storm = game.Weather is Weather.Storm or Weather.Lightning;
        var underground = game.World.Underground;
        var direction = Vector3.Normalize(new Vector3(MathF.Cos(angle), elevation, -.35f));
        var sunset = 1 - Math.Clamp(Math.Abs(elevation) * 4, 0, 1);
        var sunColor = Vector3.Lerp(new(1, .94f, .8f), new(1, .36f, .15f), sunset);
        var flash = !ReducedMotion && game.Weather == Weather.Lightning && time % 8 < .12f ? 1.5f : 0;
        water.SunDirection = direction;
        Scene.Environment = Scene.Environment with
        {
            Sky = underground ? SkyMode.Color : SkyMode.Procedural,
            Background = underground ? new(.008f, .012f, .025f, 1) : new(.1f, .25f, .35f, 1),
            SunDirection = direction,
            SkyIntensity = underground ? 0 : .08f + daylight * (storm ? .35f : .65f) + flash,
            AmbientColor = Vector3.Lerp(new(.18f, .25f, .5f), new(.6f, .73f, .8f), daylight),
            AmbientIntensity = underground ? .08f : .045f + daylight * .18f + flash,
            EnvironmentIntensity = 16 * (underground ? .005f : .02f + daylight * .2f),
            FogColor = underground ? new(.04f, .055f, .1f) : Vector3.Lerp(new(.025f, .04f, .09f), new(.4f, .57f, .62f), daylight),
            FogDensity = game.Weather == Weather.Fog ? .035f : storm ? .018f : underground ? .012f : .002f,
            SkyClouds = game.Weather == Weather.Sunny ? .42f : .85f,
            SkyHaze = game.Weather == Weather.Fog ? .9f : .3f
        };
        sun.Position = direction * 60;
        sun.LookAt(Vector3.Zero);
        sun.Light = Light.Directional(sunColor, underground ? 0 : daylight * (storm ? .5f : 1.4f) + flash);
        foreach (var lamp in nightLights) lamp.Light = Light.Point(new(1, .65f, .25f), underground || daylight < .3f ? 35 : 0, 13);
        foreach (var cloud in clouds)
        {
            var x = (cloud.Start.X + time * (storm ? 1.1f : .3f) + 90) % 180 - 90;
            cloud.Node.Position = new(x, cloud.Start.Y + MathF.Sin(time * .04f) * .3f, cloud.Start.Z);
            cloud.Node.Visible = daylight > .04f;
        }
        for (var i = 0; i < vents.Count; i++)
        {
            var size = .5f + Math.Max(0, MathF.Sin(time * .7f + i)) * 2;
            vents[i].Scale = new(1, size * 2, 1);
        }
    }
}
