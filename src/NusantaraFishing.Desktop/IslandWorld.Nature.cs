using System.Numerics;
using NusantaraFishing.Core;
using ThreeNet;
using ThreeNet.Effects;
using ThreeNet.Interop;

namespace NusantaraFishing.Desktop;

/// <summary>Island terrain, wind-blown vegetation, wildlife, villagers and cascades.</summary>
public sealed partial class IslandWorld
{
    private readonly record struct SwaySpec(Vector3 Base, float Amplitude, float Frequency, float Phase, bool Underwater);
    private sealed class SwayBatch(Node node, Matrix4x4[] relative, SwaySpec[] specs)
    {
        public Node Node { get; } = node;
        public Matrix4x4[] Relative { get; } = relative;
        public SwaySpec[] Specs { get; } = specs;
        public Matrix4x4[] Buffer { get; } = new Matrix4x4[relative.Length];
    }
    private sealed class WildAnimal(Node root, string kind, Vector2 home, Vector2 away, float phase)
    {
        public Node Root { get; } = root;
        public string Kind { get; } = kind;
        public Vector2 Home { get; } = home;
        public Vector2 Away { get; } = away;
        public float Phase { get; } = phase;
        public AnimationPlayer? Walk { get; init; }
        public AnimationPlayer? Idle { get; init; }
        public PersonVisual? Person { get; init; }
        public WakeTrail? Wake { get; init; }
        public float Pace { get; set; }
        public Vector2 Previous { get; set; } = home;
        public float Yaw { get; set; }
    }
    private sealed class Cascade(Vector3[] path, float width, Vector3 right, Vector3 outward, Node[] streaks, Node[] foam, ParticleEffect mist, Geometry mistGeometry)
    {
        /// <summary>Centre line of the falling water from lip to plunge pool.</summary>
        public Vector3[] Path { get; } = path;
        public float Width { get; } = width;
        public Vector3 Right { get; } = right;
        public Vector3 Outward { get; } = outward;
        public Node[] Streaks { get; } = streaks;
        public Node[] Foam { get; } = foam;
        public ParticleEffect Mist { get; } = mist;
        public Geometry MistGeometry { get; } = mistGeometry;
        public Vector3 Lip => Path[0];
        public Vector3 Foot => Path[^1] with { Y = 0 };
        public float DropCarry { get; set; }

        public (Vector3 Point, Vector3 Down) Along(float t)
        {
            var scaled = Math.Clamp(t, 0, 1) * (Path.Length - 1);
            var i = Math.Min((int)scaled, Path.Length - 2);
            var down = Vector3.Normalize(Path[i + 1] - Path[i]);
            return (Vector3.Lerp(Path[i], Path[i + 1], scaled - i), down);
        }
    }

    private readonly Dictionary<uint, SwaySpec> swaying = [];
    private readonly List<SwayBatch> swayBatches = [];
    private readonly List<WildAnimal> wildlife = [];
    private readonly List<Cascade> cascades = [];
    private Shader? mistShader;
    private readonly Random natureRandom = new(977);
    public int WildlifeCount => wildlife.Count;
    public int SwayingInstances => swayBatches.Sum(x => x.Relative.Length);

    private bool Tropical => game.World.Biome is Biome.Coast or Biome.Reef or Biome.Lake or Biome.Waterfall or Biome.River or Biome.Pothole or Biome.Sky;

    /// <summary>Registers every mesh under <paramref name="root"/> to bend around a base point in the wind.</summary>
    private void Sway(Node? root, Vector3 basePoint, float amplitude, float frequency = 1, bool underwater = false)
    {
        if (root is null) return;
        var phase = basePoint.X * .37f + basePoint.Z * .21f;
        foreach (var node in Descendants(root).Where(x => x.Geometry is not null))
            swaying[node.Id] = new(basePoint, amplitude, frequency * (.85f + (MathF.Abs(MathF.Sin(phase * 7.1f)) * .3f)), phase, underwater);
    }

    private Material TexturedMaterial(string file, Vector3 tint, float roughness)
    {
        var key = file + tint;
        if (materials.TryGetValue(key, out var cached)) return cached;
        var texture = Scene.LoadTextureCached(Path.Combine(AppContext.BaseDirectory, "assets/textures", file));
        texture.SetSampler(WrapMode.Repeat, WrapMode.Repeat, true, true);
        var material = Scene.CreateMaterial(MaterialOptions.Pbr(new(tint, 1), 0, roughness) with { BaseColorMap = texture });
        materials.Add(key, material);
        return material;
    }

    private static Vector3 Linear(string color)
    {
        var c = global::Avalonia.Media.Color.Parse(color);
        static float L(byte v) { var s = v / 255f; return s <= .04045f ? s / 12.92f : MathF.Pow((s + .055f) / 1.055f, 2.4f); }
        return new(L(c.R), L(c.G), L(c.B));
    }

    // ---- Island terrain ------------------------------------------------------------------

    private static float IslandHeight(Landmark island, float x, float z, out float r) => island.IslandSurface(x, z, out r);

    private static Vector3 IslandPoint(Landmark island, float r, float angle)
    {
        var rim = 1 + .06f * MathF.Sin(angle * 5 + island.X) + .03f * MathF.Sin(angle * 11 + island.Z);
        var x = island.X + MathF.Cos(angle) * r * rim * island.Width * .5f;
        var z = island.Z + MathF.Sin(angle) * r * rim * island.Depth * .5f;
        return new(x, island.IslandSurface(x, z, out _), z);
    }

    private static Vector3 IslandNormal(Landmark island, float x, float z)
    {
        const float e = .12f;
        var hx = island.IslandSurface(x + e, z, out _) - island.IslandSurface(x - e, z, out _);
        var hz = island.IslandSurface(x, z + e, out _) - island.IslandSurface(x, z - e, out _);
        return Vector3.Normalize(new(-hx / (2 * e), 1, -hz / (2 * e)));
    }

    /// <summary>
    /// One polar grid per island; each triangle picks jungle floor, bare karst or beach sand by its
    /// slope and height, so cliffs read as limestone and gentle ground as forest.
    /// </summary>
    private void IslandTerrain(Landmark island)
    {
        const int segments = 84, rings = 34;
        var lush = Tropical && game.World.Biome != Biome.Sky;
        var tint = Linear(island.Color);
        Material[] palette =
        [
            lush ? TexturedMaterial("jungle-floor.jpg", Vector3.Lerp(Vector3.One, tint * 2.2f, .25f), .92f)
                 : TexturedMaterial("wet-rock.jpg", Vector3.Lerp(new(1.5f), tint * 3, .5f), .85f),
            TexturedMaterial("wet-rock.jpg", Vector3.Lerp(new(1.7f), tint * 3, .15f), .8f),
            TexturedMaterial("coral-sand.png", Linear(game.World.SandColor) * 1.6f, .95f)
        ];
        var grid = new Vector3[(rings + 1) * (segments + 1)];
        var normals = new Vector3[grid.Length];
        for (var ring = 0; ring <= rings; ring++)
            for (var s = 0; s <= segments; s++)
            {
                // Rings crowd towards the shore where the flanks turn steep.
                var r = 1.13f * MathF.Sin(ring / (float)rings * MathF.PI / 2);
                var point = IslandPoint(island, Math.Max(r, .0001f), s / (float)segments * MathF.Tau);
                grid[ring * (segments + 1) + s] = point;
                normals[ring * (segments + 1) + s] = IslandNormal(island, point.X, point.Z);
            }
        var bands = new List<Vertex>[] { [], [], [] };
        for (var ring = 0; ring < rings; ring++)
            for (var s = 0; s < segments; s++)
            {
                var a = ring * (segments + 1) + s;
                int[][] triangles = [[a, a + 1, a + segments + 1], [a + 1, a + segments + 2, a + segments + 1]];
                // Classify whole grid quads, so material borders step cleanly instead of zig-zagging.
                var corners = new[] { grid[a], grid[a + 1], grid[a + segments + 1], grid[a + segments + 2] };
                var average = (corners[0] + corners[1] + corners[2] + corners[3]) / 4;
                var up = Vector3.Normalize(Vector3.Cross(corners[2] - corners[0], corners[1] - corners[0]) + Vector3.Cross(corners[2] - corners[1], corners[3] - corners[1]));
                var band = average.Y < .75f ? 2 : MathF.Abs(up.Y) < .62f ? 1 : 0;
                foreach (var t in triangles)
                {
                    foreach (var index in t)
                    {
                        var p = grid[index];
                        var uv = band == 1
                            ? new Vector2(MathF.Atan2(p.Z - island.Z, p.X - island.X) * (island.Width + island.Depth) * .1f, p.Y * .2f)
                            : new Vector2(p.X * .16f, p.Z * .16f);
                        bands[band].Add(new(p, normals[index], uv));
                    }
                }
            }
        for (var band = 0; band < bands.Length; band++)
        {
            if (bands[band].Count == 0) continue;
            var vertices = bands[band].ToArray();
            var indices = Enumerable.Range(0, vertices.Length).Select(x => (uint)x).ToArray();
            var node = Scene.AddMesh(Scene.CreateGeometry(vertices, indices), palette[band], name: "island-terrain");
            node.CastShadow = band != 2;
            staticMeshes.Add(node);
        }
    }

    // ---- Vegetation ------------------------------------------------------------------------

    private void Plant(string model, Vector3 position, float scale, float amplitude, float frequency = 1, float detail = 1)
    {
        var plant = Asset(model, position, new(scale));
        if (plant is null) return;
        if (detail < 1) ReduceGeometry(plant, detail);
        plant.EulerAngles = new(0, natureRandom.NextSingle() * MathF.Tau, 0);
        Sway(plant, position, amplitude, frequency);
    }

    private void IslandVegetation(Landmark island)
    {
        var area = island.Width * island.Depth;
        var lush = Tropical;
        var palms = lush && game.World.Biome != Biome.Sky ? Math.Clamp((int)(area / 32), 3, 10) : 0;
        var bananas = lush ? Math.Clamp((int)(area / 45), 2, 8) : 0;
        var undergrowth = Math.Clamp((int)(area / (lush ? 7 : 25)), 6, 34);
        // Trees need gentle ground; undergrowth also clings to the steep karst flanks.
        Vector3 Spot(float inner, float outer, float flattest)
        {
            var best = IslandPoint(island, inner, 0);
            for (var attempt = 0; attempt < 8; attempt++)
            {
                best = IslandPoint(island, inner + natureRandom.NextSingle() * (outer - inner), natureRandom.NextSingle() * MathF.Tau);
                if (best.Y > .8f && IslandNormal(island, best.X, best.Z).Y >= flattest) break;
            }
            return best;
        }
        for (var i = 0; i < palms; i++) Palm(Spot(.05f, .85f, .5f), .9f + natureRandom.NextSingle() * .5f);
        for (var i = 0; i < bananas; i++) Plant("banana-plant", Spot(.05f, .8f, .45f), 1 + natureRandom.NextSingle() * .45f, .05f, 1.3f);
        for (var i = 0; i < undergrowth; i++)
            Plant("wild-undergrowth", Spot(0, .96f, .3f) - new Vector3(0, .1f, 0), 1.1f + natureRandom.NextSingle() * 1.2f, .09f, 1.7f, .5f);
    }

    private void BankVegetation(Landmark bank)
    {
        if (!Tropical && game.World.Biome != Biome.ThreeLakes) return;
        var area = bank.Width * bank.Depth;
        var sparse = game.World.Biome is Biome.ThreeLakes or Biome.Pothole;
        var undergrowth = Math.Clamp((int)(area / (sparse ? 90 : 40)), 4, 24);
        var bananas = sparse ? 0 : Math.Clamp((int)(area / 160), 2, 8);
        Vector3 Spot() => new(bank.X + (natureRandom.NextSingle() - .5f) * bank.Width * .84f, bank.Height,
            bank.Z + (natureRandom.NextSingle() - .5f) * bank.Depth * .84f);
        for (var i = 0; i < bananas; i++)
        {
            var spot = Spot();
            if (!game.World.CanWalk(new(spot.X, spot.Z))) continue;
            Plant("banana-plant", spot, .85f + natureRandom.NextSingle() * .35f, .05f, 1.3f);
        }
        for (var i = 0; i < undergrowth; i++) Plant("wild-undergrowth", Spot(), .9f + natureRandom.NextSingle() * 1, .09f, 1.7f, .6f);
    }

    /// <summary>Kalimantan canopy tree: tall trunk, buttress roots and a broken crown of leaf clumps.</summary>
    private void RainforestTree(Vector3 basePoint, int index)
    {
        var height = 6.5f + index % 3 * 1.4f;
        var bark = TexturedMaterial("wet-rock.jpg", new(.55f, .42f, .3f), .95f);
        var leaves = TexturedMaterial("jungle-floor.jpg", Vector3.Lerp(Vector3.One, Linear(game.World.VegetationColor) * 3, .35f), .85f);
        void Part(Geometry geometry, Material material, Vector3 offset, Vector3 scale, Vector3 euler = default)
        {
            var node = Scene.AddMesh(geometry, material);
            node.Position = basePoint + offset; node.Scale = scale; node.EulerAngles = euler;
            staticMeshes.Add(node);
            Sway(node, basePoint, .014f, .55f);
        }
        Part(cylinder, bark, new(0, height * .5f, 0), new(.42f, height, .42f));
        for (var i = 0; i < 3; i++)
        {
            var angle = i * 2.1f + index;
            Part(box, bark, new(MathF.Cos(angle) * .35f, .45f, MathF.Sin(angle) * .35f), new(.12f, .9f, .7f), new(0, -angle, .35f));
        }
        for (var i = 0; i < 5; i++)
        {
            var angle = i * 1.26f + index * .7f;
            var spread = i == 0 ? 0 : 1.5f + (i % 2) * .5f;
            var size = i == 0 ? 2.4f : 1.5f + (i % 3) * .35f;
            Part(sphere, leaves, new(MathF.Cos(angle) * spread, height + (i == 0 ? .8f : (i % 2) * .5f - .2f), MathF.Sin(angle) * spread), new(size, size * .62f, size));
        }
    }

    private void BatchSwayingVegetation()
    {
        var specs = staticMeshes.Where(x => swaying.ContainsKey(x.Id) && x.Geometry is not null && x.Material is not null).ToArray();
        foreach (var group in specs.GroupBy(x => (Geometry: x.Geometry!.Id, Material: x.Material!.Id, Cast: x.CastShadow, Receive: x.ReceiveShadow)))
        {
            var originals = group.ToArray();
            var first = originals[0];
            var relative = originals.Select(x => x.WorldMatrix * Matrix4x4.CreateTranslation(-swaying[x.Id].Base)).ToArray();
            var batch = Scene.AddMesh(first.Geometry!, first.Material!, name: "swaying-vegetation");
            batch.CastShadow = group.Key.Cast; batch.ReceiveShadow = group.Key.Receive;
            var swayBatch = new SwayBatch(batch, relative, originals.Select(x => swaying[x.Id]).ToArray());
            swayBatches.Add(swayBatch);
            UpdateSwayBatch(swayBatch, 0, 1, Vector2.UnitX, true);
            foreach (var original in originals) { Scene.Remove(original); staticMeshes.Remove(original); }
        }
    }

    private void UpdateSwayBatch(SwayBatch batch, float time, float wind, Vector2 direction, bool initial)
    {
        // Bend away from the wind about each plant's base: a steady lean plus two gust harmonics.
        var axis = Vector3.Normalize(new(direction.Y, 0, -direction.X));
        var across = new Vector3(direction.X, 0, direction.Y);
        for (var i = 0; i < batch.Relative.Length; i++)
        {
            var spec = batch.Specs[i];
            var strength = spec.Underwater ? 1 : wind;
            var t = time * spec.Frequency + spec.Phase;
            var gust = .55f + .45f * MathF.Sin(time * .23f + spec.Base.X * .03f + spec.Base.Z * .02f);
            var bend = spec.Amplitude * strength * (.35f * gust + MathF.Sin(t * 1.3f) * .5f * gust + MathF.Sin(t * 3.1f + 1.7f) * .15f);
            var twist = spec.Amplitude * strength * MathF.Sin(t * 1.9f + .6f) * .35f;
            var rotation = Matrix4x4.CreateFromAxisAngle(axis, bend) * Matrix4x4.CreateFromAxisAngle(across, twist);
            batch.Buffer[i] = batch.Relative[i] * rotation * Matrix4x4.CreateTranslation(spec.Base);
        }
        if (initial) sceneCache.SetInstances(batch.Node, batch.Buffer);
        else batch.Node.SetInstances(batch.Buffer);
    }

    private float WindStrength => game.World.Underground ? 0 : game.Weather switch
    {
        Weather.Storm or Weather.Lightning => 2.6f,
        Weather.Rain => 1.6f,
        Weather.Fog => .45f,
        _ => 1
    };

    private Vector2 WindDirection(float time)
    {
        var angle = .6f + MathF.Sin(time * .015f) * .5f;
        return new(MathF.Cos(angle), MathF.Sin(angle));
    }

    // ---- Wildlife and villagers --------------------------------------------------------------

    private float SurfaceHeight(Vector2 point)
    {
        foreach (var feature in game.World.Landmarks)
        {
            if (feature.Kind == LandmarkKind.Island)
            {
                var h = IslandHeight(feature, point.X, point.Y, out var r);
                if (r < 1.2f) return h;
            }
            else if (feature.Kind is LandmarkKind.Bank or LandmarkKind.Road or LandmarkKind.Bridge && feature.Contains(point, -.3f))
                return game.World.GroundHeight(point) - (feature.Kind == LandmarkKind.Bank ? .35f : 0);
        }
        return game.World.GroundHeight(point);
    }

    private void BuildWildlife()
    {
        var world = game.World;
        if (world.Underground) return;
        var islands = world.Landmarks.Where(x => x.Kind == LandmarkKind.Island).ToArray();
        var banks = world.Landmarks.Where(x => x.Kind == LandmarkKind.Bank && x.Width * x.Depth > 60).ToArray();
        var wild = Tropical || world.Biome is Biome.ThreeLakes;
        // Macaques forage on island crowns and along wooded banks.
        if (wild)
        {
            var homes = islands.Select(x => (Center: IslandPoint(x, .25f, natureRandom.NextSingle() * MathF.Tau), Range: Math.Min(x.Width, x.Depth) * .18f))
                .Concat(banks.Select(x => (Center: new Vector3(x.X + (natureRandom.NextSingle() - .5f) * x.Width * .5f, x.Height, x.Z), Range: Math.Min(x.Depth * .3f, 4))))
                .Take(4).ToArray();
            for (var i = 0; i < homes.Length; i++)
            {
                var root = Scene.CreateNode(name: "wild-macaque");
                var path = Path.Combine(AppContext.BaseDirectory, "assets/models/macaque.glb");
                if (!File.Exists(path)) break;
                var previous = Scene.Animations.Select(x => x.Id).ToHashSet();
                Scene.LoadGltf(path, root);
                var clips = Scene.Animations.Where(x => !previous.Contains(x.Id)).ToArray();
                var walk = clips.FirstOrDefault(x => x.Name == "Walk")?.Play(weight: 0);
                var idle = clips.FirstOrDefault(x => x.Name == "Idle")?.Play();
                if (idle is not null) idle.Time = i * 1.3f;
                var center = new Vector2(homes[i].Center.X, homes[i].Center.Z);
                wildlife.Add(new(root, "macaque", center, new(Math.Max(1.2f, homes[i].Range), 0), i * 2.1f) { Walk = walk, Idle = idle });
            }
        }
        // Monitor lizards bask on the beach and swim short loops offshore.
        if (wild && world.Biome != Biome.Sky)
        {
            var shores = islands.Select(x =>
                {
                    var angle = MathF.Atan2(-x.Z / (x.Depth * .5f), -x.X / (x.Width * .5f)) + .5f;
                    var land = IslandPoint(x, 1, angle);
                    var sea = IslandPoint(x, 1 + 9f / Math.Max(x.Width, x.Depth), angle);
                    return (Land: new Vector2(land.X, land.Z), Sea: new Vector2(sea.X, sea.Z));
                })
                .Concat(banks.Select(x =>
                {
                    // The bank point nearest open water at the origin, just inside its edge.
                    var edge = new Vector2(Math.Clamp(0, x.X - x.Width / 2 + 1, x.X + x.Width / 2 - 1), Math.Clamp(0, x.Z - x.Depth / 2 + 1, x.Z + x.Depth / 2 - 1));
                    var outward = Vector2.Normalize(-edge + new Vector2(.001f));
                    return (Land: edge, Sea: edge + outward * 4.5f);
                }))
                .Where(x => game.World.CanSail(x.Sea, .5f)).Take(3).ToArray();
            for (var i = 0; i < shores.Length; i++)
            {
                var root = Scene.CreateNode(name: "wild-monitor-lizard");
                if (CatchModel("biawak", root, 1.9f) is null) break;
                wildlife.Add(new(root, "monitor", shores[i].Land, shores[i].Sea, i * 7.3f) { Wake = new(.45f, 1.1f, .36f, .8f, .25f, .8f) });
            }
        }
        // Villagers stroll along the banks and harbor roads.
        var walkways = world.Landmarks.Where(x => x.Kind is LandmarkKind.Bank or LandmarkKind.Road && x.Width * x.Depth > 80).Take(3).ToArray();
        string[] villagers = ["ujang", "rina", "bakso", "pendaki"];
        string[] outfits = ["#B98C58", "#64AA7D", "#C36A50", "#5E8A9A"];
        for (var i = 0; i < walkways.Length; i++)
        {
            var lane = walkways[i];
            var alongX = lane.Width >= lane.Depth;
            var half = (alongX ? lane.Width : lane.Depth) * .35f;
            var offset = (natureRandom.NextSingle() - .5f) * (alongX ? lane.Depth : lane.Width) * .4f;
            var a = alongX ? new Vector2(lane.X - half, lane.Z + offset) : new Vector2(lane.X + offset, lane.Z - half);
            var b = alongX ? new Vector2(lane.X + half, lane.Z + offset) : new Vector2(lane.X + offset, lane.Z + half);
            if (!world.CanWalk(a) || !world.CanWalk(b)) continue;
            var root = Scene.CreateNode(name: "villager");
            var person = Person(root, outfits[i % outfits.Length], character: villagers[i % villagers.Length]);
            wildlife.Add(new(root, "villager", a, b, i * 3.7f) { Person = person });
        }
    }

    private void UpdateWildlife(float dt, float time)
    {
        var daylight = game.Player.WorldHours is >= 6 and < 19;
        foreach (var animal in wildlife)
        {
            Vector2 point;
            float height;
            var moving = true;
            switch (animal.Kind)
            {
                case "macaque":
                {
                    // Forage: wander for seven seconds, sit and look around for four.
                    var cycle = (time + animal.Phase) % 11;
                    moving = cycle < 7;
                    var walked = MathF.Floor((time + animal.Phase) / 11) * 7 + Math.Min(cycle, 7);
                    var radius = animal.Away.X;
                    var angle = walked * .55f / radius;
                    point = animal.Home + new Vector2(MathF.Cos(angle), MathF.Sin(angle) * .7f) * radius;
                    height = SurfaceHeight(point);
                    break;
                }
                case "monitor":
                {
                    // Bask, crawl into the water, swim a short arc, return.
                    var cycle = (time + animal.Phase) % 34;
                    var s = cycle < 10 ? 0 : cycle < 16 ? (cycle - 10) / 6 : cycle < 22 ? 1 : cycle < 28 ? 1 - (cycle - 22) / 6 : 0;
                    moving = cycle is >= 10 and < 28;
                    s = s * s * (3 - 2 * s);
                    var swing = cycle is >= 16 and < 22 ? MathF.Sin((cycle - 16) / 6 * MathF.PI) * 1.8f : 0;
                    var direction = Vector2.Normalize(animal.Away - animal.Home);
                    point = Vector2.Lerp(animal.Home, animal.Away, s) + new Vector2(-direction.Y, direction.X) * swing;
                    var ground = SurfaceHeight(point);
                    var swimming = ground < -.05f || !game.World.CanWalk(point) && s > .35f;
                    height = swimming ? -.06f : Math.Max(ground, 0) + .1f;
                    if (animal.Wake is { } wake)
                    {
                        if (swimming && moving) wake.Update(water, new(point.X, 0, point.Y), dt); else wake.Reset();
                    }
                    break;
                }
                default:
                {
                    var length = Vector2.Distance(animal.Home, animal.Away);
                    var s = .5f + .5f * MathF.Sin((time + animal.Phase) * .9f / Math.Max(1, length));
                    point = Vector2.Lerp(animal.Home, animal.Away, s);
                    height = game.World.GroundHeight(point);
                    break;
                }
            }
            var step = point - animal.Previous;
            if (step.LengthSquared() > 1e-6f) animal.Yaw = TurnToward(animal.Yaw, MathF.Atan2(-step.X, -step.Y), 1 - MathF.Exp(-dt * 8));
            animal.Previous = point;
            animal.Pace += ((moving ? 1 : 0) - animal.Pace) * (1 - MathF.Exp(-dt * 5));
            if (animal.Walk is { } walk) walk.Weight = animal.Pace;
            if (animal.Idle is { } idle) idle.Weight = 1 - animal.Pace;
            if (animal.Kind == "monitor" && catchAnimations.TryGetValue(animal.Root.Id, out var swim))
                foreach (var (_, player) in swim) player.Speed = .25f + animal.Pace * .9f;
            if (animal.Person is { } person) Walk(person, time, 1);
            animal.Root.Visible = animal.Kind != "villager" || daylight;
            animal.Root.Position = new(point.X, height, point.Y);
            animal.Root.EulerAngles = new(0, animal.Yaw, 0);
        }
    }

    // ---- Cascades ----------------------------------------------------------------------------

    private void AddCascade(Vector3[] path, float width, Vector3 outward)
    {
        mistShader ??= Scene.CreateShader(EffectShaders.Smoke, ShaderLanguage.Wgsl, "nusantara-waterfall-mist");
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, outward));
        var white = Scene.CreateMaterial(MaterialOptions.Pbr(new(.92f, .97f, 1, .42f), 0, .2f) with
        { AlphaMode = AlphaMode.Blend, CullMode = CullMode.None, DepthWrite = false });
        var height = path[0].Y - path[^1].Y;
        // Many thin, overlapping ribbons falling at different rates read as moving water.
        var streaks = new Node[Math.Clamp((int)(width * 2.6f), 6, 40)];
        for (var i = 0; i < streaks.Length; i++)
        {
            streaks[i] = Scene.AddMesh(box, white, name: "waterfall-streak");
            streaks[i].CastShadow = false;
            streaks[i].Scale = new(.12f + natureRandom.NextSingle() * .3f, Math.Clamp(height * (.2f + natureRandom.NextSingle() * .2f), 1, 7), .05f);
        }
        var foam = new Node[Math.Clamp((int)(width * 1.2f), 4, 18)];
        for (var i = 0; i < foam.Length; i++)
        {
            foam[i] = Scene.AddMesh(sphere, white, name: "plunge-foam");
            foam[i].CastShadow = false;
        }
        var foot = path[^1] with { Y = 0 };
        var mist = new ParticleEffect(320, 53 + cascades.Count)
        {
            Rate = Math.Clamp(width * 7, 18, 110),
            Lifetime = 3.6f,
            Size = Math.Clamp(width * .2f, 1.3f, 3.2f),
            Growth = 3.4f,
            Shape = EmitterShape.Box,
            EmitterSize = new(Math.Abs(right.X) * width + .8f, .4f, Math.Abs(right.Z) * width + .8f),
            Origin = foot + new Vector3(0, .4f, 0) + outward * .6f,
            Rise = 1.2f,
            Spread = 1.3f,
            Direction = Vector3.Normalize(outward * .7f + Vector3.UnitY),
            Gravity = new(0, -.05f, 0),
            Drag = .85f,
            Turbulence = .8f
        };
        var geometry = mist.CreateGeometry(Scene);
        var mistMaterial = Scene.CreateMaterial(ParticleEffect.SmokeMaterial(mistShader, new(.92f, .96f, .98f), .38f));
        Scene.AddMesh(geometry, mistMaterial, name: "waterfall-mist").CastShadow = false;
        cascades.Add(new(path, width, right, outward, streaks, foam, mist, geometry));
    }

    /// <summary>A narrow stream tumbling down the flank of the tallest tropical island.</summary>
    private void IslandCascade()
    {
        if (game.World.Biome is not (Biome.Coast or Biome.Reef or Biome.Lake or Biome.Waterfall)) return;
        var island = game.World.Landmarks.Where(x => x.Kind == LandmarkKind.Island && x.Height >= 9).OrderByDescending(x => x.Height).FirstOrDefault();
        if (island is null) return;
        var angle = MathF.Atan2(-island.Z / (island.Depth * .5f), -island.X / (island.Width * .5f)) - .35f;
        const int steps = 14;
        var path = new Vector3[steps + 1];
        for (var i = 0; i <= steps; i++)
        {
            var point = IslandPoint(island, float.Lerp(.72f, 1.03f, i / (float)steps), angle);
            path[i] = point + IslandNormal(island, point.X, point.Z) * .3f;
        }
        path[^1] = path[^1] with { Y = Math.Max(path[^1].Y, -.05f) };
        var outward = Vector3.Normalize((path[^1] - path[0]) with { Y = 0 });
        var right = Vector3.Normalize(Vector3.Cross(Vector3.UnitY, outward));
        // A ribbon draped over the slope: the stream bed the streaks run down.
        const float width = 1.8f;
        var vertices = new Vertex[path.Length * 2];
        for (var i = 0; i < path.Length; i++)
        {
            var normal = IslandNormal(island, path[i].X, path[i].Z);
            for (var side = 0; side < 2; side++)
            {
                var p = path[i] + right * ((side - .5f) * width * (.75f + .25f * i / steps));
                vertices[i * 2 + side] = new(p, normal, new(side, i / (float)steps));
            }
        }
        var indices = new List<uint>();
        for (var i = 0; i < steps; i++)
        {
            var a = (uint)(i * 2);
            indices.AddRange([a, a + 2, a + 1, a + 1, a + 2, a + 3]);
        }
        var bed = Scene.CreateMaterial(MaterialOptions.Pbr(new(.42f, .74f, .82f, .7f), 0, .1f) with
        { AlphaMode = AlphaMode.Blend, CullMode = CullMode.None, DepthWrite = false });
        Scene.AddMesh(Scene.CreateGeometry(vertices, indices.ToArray()), bed, name: "island-stream").CastShadow = false;
        AddCascade(path, width, outward);
    }

    private void UpdateCascades(float dt, float time)
    {
        if (cascades.Count == 0) return;
        var camera = Camera.WorldMatrix;
        var wind = WindDirection(time) * .3f * WindStrength;
        foreach (var cascade in cascades)
        {
            var count = cascade.Streaks.Length;
            for (var i = 0; i < count; i++)
            {
                var streak = cascade.Streaks[i];
                // Each ribbon slides from the lip, accelerating like falling water, then restarts.
                var rate = .45f + (i * .618f % 1) * .35f;
                var cycle = (time * rate + i * .37f) % 1;
                var (point, down) = cascade.Along(cycle * cycle);
                var across = ((i * .618f % 1) - .5f) * cascade.Width * .9f;
                var up = -down;
                var forward = Vector3.Normalize(Vector3.Cross(cascade.Right, up));
                streak.Rotation = Quaternion.CreateFromRotationMatrix(new Matrix4x4(
                    cascade.Right.X, cascade.Right.Y, cascade.Right.Z, 0, up.X, up.Y, up.Z, 0, forward.X, forward.Y, forward.Z, 0, 0, 0, 0, 1));
                // The ribbon hangs below its leading point, so nothing pokes above the lip.
                streak.Position = point + cascade.Right * across + forward * .12f + down * streak.Scale.Y * .5f;
                streak.Visible = point.Y + down.Y * streak.Scale.Y > -.2f;
            }
            for (var i = 0; i < cascade.Foam.Length; i++)
            {
                // Churning foam: blobs swell and collapse out of phase across the plunge pool.
                var churn = .5f + .5f * MathF.Sin(time * (2.3f + i * .37f) + i * 1.7f);
                var across = ((i + .5f) / cascade.Foam.Length - .5f) * cascade.Width;
                var drift = (time * .4f + i * .29f) % 1;
                cascade.Foam[i].Position = cascade.Foot + cascade.Right * across + cascade.Outward * (.4f + drift * 1.6f) + new Vector3(0, .05f, 0);
                var size = (.35f + churn * .55f) * (1 - drift * .6f) * Math.Clamp(cascade.Width * .12f, .6f, 1.6f);
                cascade.Foam[i].Scale = new(size, size * .3f, size);
            }
            // Spray pocks the plunge pool continuously, spreading rings outward.
            cascade.DropCarry += dt * Math.Clamp(cascade.Width * 4, 6, 40);
            while (cascade.DropCarry >= 1)
            {
                cascade.DropCarry -= 1;
                var x = (natureRandom.NextSingle() - .5f) * cascade.Width;
                water.Splash(cascade.Foot + cascade.Right * x + cascade.Outward * (.5f + natureRandom.NextSingle()), 1.1f, .07f);
            }
            cascade.Mist.Wind = new(wind.X, 0, wind.Y);
            cascade.Mist.Update(ReducedMotion ? dt * .3f : dt, time);
            cascade.Mist.Upload(cascade.MistGeometry, camera);
        }
    }

    internal IEnumerable<(string Kind, Vector3 Position)> WildlifePositions() => wildlife.Select(x => (x.Kind, x.Root.Position));
    /// <summary>Review probe: largest simulated ripple height (field units) within a radius.</summary>
    internal float RippleNear(Vector2 point, float radius)
    {
        var simulation = water.Simulation;
        var cells = simulation.Cells;
        var n = simulation.Resolution;
        var cell = simulation.Size / n;
        var cx = (int)((point.X - simulation.Center.X) / simulation.Size * n + n / 2f);
        var cz = (int)((point.Y - simulation.Center.Z) / simulation.Size * n + n / 2f);
        var reach = (int)MathF.Ceiling(radius / cell);
        var peak = 0f;
        for (var z = Math.Max(0, cz - reach); z <= Math.Min(n - 1, cz + reach); z++)
            for (var x = Math.Max(0, cx - reach); x <= Math.Min(n - 1, cx + reach); x++)
                peak = Math.Max(peak, MathF.Abs(cells[(z * n + x) * 4]));
        return peak;
    }

    internal IEnumerable<Vector3> CascadeTargets() => cascades.Select(x => x.Along(.5f).Point);

    private void UpdateNature(float dt, float time)
    {
        var wind = WindStrength;
        var direction = WindDirection(time);
        foreach (var batch in swayBatches) UpdateSwayBatch(batch, time, wind, direction, false);
        UpdateWildlife(dt, time);
        UpdateCascades(dt, time);
    }
}
