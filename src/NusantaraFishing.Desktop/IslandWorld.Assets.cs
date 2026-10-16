using System.Numerics;
using ThreeNet;
using ThreeNet.Interop;

namespace NusantaraFishing.Desktop;

public sealed partial class IslandWorld
{
    private int residentModelIndex;
    private readonly Dictionary<(uint Id, float Ratio), Geometry> simplifiedGeometry = [];

    private void ReduceGeometry(Node root, float ratio, bool castShadow = true)
    {
        foreach (var node in Descendants(root).Where(x => x.Geometry is not null))
        {
            var geometry = node.Geometry!;
            var key = (geometry.Id, ratio);
            if (!simplifiedGeometry.TryGetValue(key, out var simplified))
            {
                simplified = geometry.Simplify(ratio, ratio < .3f ? .08f : .025f).Simplified;
                simplifiedGeometry.Add(key, simplified);
            }
            node.AttachMesh(simplified, node.Material!);
            node.CastShadow = castShadow;
        }
    }

    private void ShoreBank(Vector3 position, Vector3 size, string color)
    {
        const int segments = 32, stride = segments + 1;
        var vertices = new Vertex[stride * stride];
        var indices = new List<uint>(segments * segments * 6);
        float Height(float x, float z)
        {
            var edge = Math.Max(Math.Abs(x), Math.Abs(z));
            var irregular = .022f * MathF.Sin(x * 21 + z * 8) * MathF.Sin(z * 17);
            var t = Math.Clamp((1 - edge + irregular) / .15f, 0, 1);
            return -1 + (size.Y + 1) * t * t * (3 - 2 * t);
        }
        for (var z = 0; z <= segments; z++)
            for (var x = 0; x <= segments; x++)
            {
                var px = x / (float)segments * 2 - 1;
                var pz = z / (float)segments * 2 - 1;
                var normal = Vector3.Normalize(new Vector3((Height(px - .01f, pz) - Height(px + .01f, pz)) / (size.X * .01f), 1,
                    (Height(px, pz - .01f) - Height(px, pz + .01f)) / (size.Z * .01f)));
                var point = position + new Vector3(px * size.X * .5f, Height(px, pz), pz * size.Z * .5f);
                vertices[z * stride + x] = new(point, normal, new(point.X * .14f, point.Z * .14f));
                if (x < segments && z < segments)
                {
                    var a = (uint)(z * stride + x);
                    indices.AddRange([a, a + stride, a + 1, a + 1, a + stride, a + stride + 1]);
                }
            }
        var texture = Scene.LoadTextureCached(Path.Combine(AppContext.BaseDirectory, "assets/textures/coral-sand.png"));
        var options = game.World.Biome == NusantaraFishing.Core.Biome.Coast
            ? MaterialOptions.Pbr(new(.78f, .76f, .64f, 1), 0, .95f) with { BaseColorMap = texture }
            : game.World.Biome is NusantaraFishing.Core.Biome.River or NusantaraFishing.Core.Biome.Lake or NusantaraFishing.Core.Biome.Pothole
                ? TexturedMaterial("jungle-floor.jpg", Vector3.Lerp(Vector3.One, Linear(color) * 2.4f, .3f), .92f).Options
                : Mat(color).Options;
        var node = Scene.AddMesh(Scene.CreateGeometry(vertices, indices.ToArray()), Scene.CreateMaterial(options), name: "sculpted-shoreline");
        staticMeshes.Add(node);
    }

    private static IEnumerable<Node> Descendants(Node root)
    {
        yield return root;
        foreach (var child in root.Children)
            foreach (var node in Descendants(child)) yield return node;
    }

    private Node? Asset(string name, Vector3 position, Vector3 scale, Node? parent = null, bool isStatic = true, bool smallRock = false)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets/models", name + ".glb");
        if (!File.Exists(path)) return null;
        try
        {
            var root = Scene.LoadModelCached(path, parent).Root;
            root.Position = position;
            root.Scale = scale;
            if (smallRock) ReduceGeometry(root, .22f, false);
            if (name == "jukung") ReduceGeometry(root, .2f);
            if (isStatic) staticMeshes.AddRange(Descendants(root).Where(x => x.Geometry is not null));
            return root;
        }
        catch (Exception ex) when (ex is ThreeNetException or IOException)
        {
            AssetWarning = $"{name}: {ex.Message}";
            return null;
        }
    }

    private PersonVisual? DetailedPerson(Node parent, string character)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "assets/models", (character == "ujang" ? "fisherman" : character) + ".glb");
        if (!File.Exists(path)) return null;
        try
        {
            var previous = Scene.Animations.Select(x => x.Id).ToHashSet();
            var model = Scene.LoadGltf(path, parent);
            model.Root.EulerAngles = new(0, MathF.PI, 0);
            var clips = Scene.Animations.Where(x => !previous.Contains(x.Id)).ToArray();
            var animator = new HumanAnimation(clips, residentModelIndex * .31f);
            // Legacy limb handles are inert for rigged people; all movement comes from their skeleton.
            var handles = Enumerable.Range(0, 4).Select(_ => Scene.CreateNode(parent)).ToArray();
            return new(parent, handles[0], handles[1], handles[2], handles[3], animator);
        }
        catch (Exception ex) when (ex is ThreeNetException or IOException)
        {
            AssetWarning = ex.Message;
            return null;
        }
    }

    private sealed class HumanAnimation
    {
        private readonly AnimationClip[] clips;
        private readonly AnimationPlayer idle;
        private readonly AnimationPlayer walk;
        private readonly AnimationPlayer fishing;
        private float movement;
        private float action;

        public HumanAnimation(AnimationClip[] clips, float phase)
        {
            this.clips = clips;
            idle = clips.Single(x => x.Name == "Idle").Play();
            walk = clips.Single(x => x.Name == "Walk").Play(weight: 0);
            fishing = clips.Single(x => x.Name == "Fishing").Play(weight: 0);
            idle.Time = phase % 3;
            walk.Time = phase % 1;
        }

        public void Pose(float speed, bool casting = false)
        {
            // Smooth pose changes over several frames instead of snapping limbs.
            movement += (Math.Clamp(speed, 0, 1) - movement) * .18f;
            action += ((casting ? 1 : 0) - action) * .14f;
            idle.Weight = (1 - movement) * (1 - action);
            walk.Weight = movement * (1 - action);
            walk.Speed = .65f + speed * .35f;
            fishing.Weight = action;
        }

        public void Stop()
        {
            idle.Stop(); walk.Stop(); fishing.Stop();
            foreach (var clip in clips) clip.Destroy();
        }
    }
}
