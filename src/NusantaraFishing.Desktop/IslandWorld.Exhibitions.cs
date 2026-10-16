using System.Numerics;
using NusantaraFishing.Core;
using ThreeNet;

namespace NusantaraFishing.Desktop;

public sealed partial class IslandWorld
{
    private Node exhibitRoot = null!;
    private int exhibitRevision = -1;
    private readonly List<Node> exhibitFish = [];

    private void BuildExhibition()
    {
        if (Asset("harbor-pavilion", new(-20, .6f, -8), Vector3.One) is not null)
        {
            Asset("harbor-pavilion", new(-10.5f, .6f, -8), Vector3.One);
            Mesh(box, "#90794D", new(-15, .3f, -8), new(21, .6f, 10));
        }
        else
        {
        Mesh(box, "#AA986F", new(-15, .3f, -8), new(21, .6f, 10));
        // Open facades let the player walk among actual exhibit placements.
        foreach (var x in new[] { -24.5f, -16f, -5.5f }) Mesh(box, "#B8AC8D", new(x, 2, -8), new(.25f, 3, x == -16 ? 7 : 9));
        Mesh(box, "#AAAB97", new(-15, 2, -12.7f), new(20, 3, .3f));
        Mesh(box, "#568388", new(-10.5f, 4, -8), new(10, .3f, 10));
        Mesh(box, "#8F6354", new(-20, 4, -8), new(9, .3f, 10));
        foreach (var x in new[] { -22f, -18f, -13f, -8f }) Mesh(cylinder, "#B09A78", new(x, 2, -3.7f), new(.18f, 3.8f, .18f));
        }
        var glass = Scene.CreateMaterial(MaterialOptions.Pbr(new(.1f, .5f, .55f, .25f), 0, .1f) with
        { AlphaMode = AlphaMode.Blend, DepthWrite = false, Transmission = .4f, Thickness = .1f, Ior = 1.33f });
        for (var i = 0; i < 4; i++)
        {
            var x = -13.5f + (i % 2) * 4;
            var z = -10.5f + (i / 2) * 4;
            Mesh(box, "#497980", new(x, .9f, z), new(2.5f, .6f, 2.5f));
            var tank = Scene.AddMesh(box, glass); tank.Position = new(x, 1.8f, z); tank.Scale = new(2.6f, 1.5f, 2.6f);
        }
    }

    private void UpdateExhibition(float time)
    {
        var hash = new HashCode();
        hash.Add(game.Player.AquariumLevel); hash.Add(game.Player.MuseumLevel);
        foreach (var trophy in game.Player.Trophies) hash.Add(trophy);
        foreach (var pair in game.Player.ExhibitSlots.OrderBy(x => x.Key)) { hash.Add(pair.Key); hash.Add(pair.Value); }
        var revision = hash.ToHashCode();
        if (revision != exhibitRevision)
        {
            if (exhibitRoot is not null) ReleaseCatch(exhibitRoot);
            exhibitFish.Clear();
            exhibitRoot = Scene.CreateNode(name: "placed-exhibits");
            var trophyIndex = 0;
            foreach (var trophy in game.Player.Trophies.TakeLast(4))
            {
                var root = Scene.CreateNode(exhibitRoot, name: "trophy-" + trophy.Period);
                Asset("tournament-trophy", Vector3.Zero, new(.7f), root, false);
                root.Position = new(-23 + trophyIndex++ * 1.8f, 1.2f, -5);
                Mesh(box, "#786C58", new(0, -.3f, 0), new(.65f, .6f, .65f), root);
            }
            foreach (var item in game.Player.ExhibitSlots)
            {
                var museum = item.Value >= 100;
                var index = item.Value % 100;
                var tank = index % 4;
                var school = index / 4;
                var x = museum ? -23 + (index % 4) * 1.8f : -13.5f + (tank % 2) * 4 + MathF.Sin(school * 2.4f) * .5f;
                var z = museum ? -11 + (index / 4) * 1.2f : -10.5f + (tank / 2) * 4 + MathF.Cos(school * 2.4f) * .5f;
                var root = Scene.CreateNode(exhibitRoot, name: "exhibit-" + item.Key);
                CatchModel(item.Key, root, museum ? .95f : .7f);
                root.Position = new(x, museum ? 1.5f : 1.35f + school * .2f, z);
                if (museum)
                {
                    Mesh(box, "#BCB097", new(0, -.55f, 0), new(.7f, .8f, .7f), root);
                }
                else
                {
                    exhibitFish.Add(root);
                }
            }
            exhibitRevision = revision;
        }
        for (var i = 0; i < exhibitFish.Count; i++) exhibitFish[i].EulerAngles = new(0, MathF.Sin(time * .6f + i) * .7f, 0);
    }
}
