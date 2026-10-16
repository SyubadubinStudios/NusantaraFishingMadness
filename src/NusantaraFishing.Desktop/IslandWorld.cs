using System.Numerics;
using NusantaraFishing.Core;
using ThreeNet;
using ThreeNet.Avalonia;
using ThreeNet.Effects;

namespace NusantaraFishing.Desktop;

/// <summary>Owns GPU resources for an expedition; shares geometry and materials.</summary>
public sealed partial class IslandWorld : IDisposable
{
    public Scene Scene { get; } = new();
    public Node Camera { get; }
    public Node Boat { get; }
    private readonly Node sun;
    private readonly Node bobber;
    private readonly Node rain;
    private readonly WaterSurface water;
    private readonly List<Node> fish = [];
    private readonly Dictionary<string, Material> materials = [];
    private readonly Geometry box;
    private readonly Geometry sphere;
    private readonly Geometry cylinder;
    private readonly Geometry cone;
    private readonly Node fishingLine;
    private readonly OrbitController orbit;
    private readonly ThreeNetView view;
    private readonly GameSession game;
    private readonly TerrainStreamer terrain;
    private readonly SceneCacheCompatibility sceneCache;
    private Node boatHull = null!;
    private Node angler = null!;
    private Node bossVisual = null!;
    private int boatVisualIndex = -1;
    private string characterVisualId = "";
    private Vector3 boatPosition = new(0, .2f, 5);
    private float boatYaw = float.NaN;
    private readonly WakeTrail boatWake = new(.6f, 1.5f, .8f, fullSpeed: 4.5f);
    private readonly FloatingBody boatFloat = new(2, 1.9f);
    private FishingPhase previousPhase;
    private float lastDt;
    private float ambientTime;
    public bool ReducedMotion { get; set; }
    public float WaterClarity { get; set; } = .075f;
    public float WaveStrength { get; set; } = .035f;
    public string? AssetWarning { get; private set; }

    internal IslandWorld(ThreeNetView view, GameSession game, SceneCacheCompatibility? sceneCache = null)
    {
        this.view = view;
        this.game = game;
        this.sceneCache = sceneCache ?? new();
        try
        {
            box = Scene.CreateBoxGeometry();
            sphere = Scene.CreateSphereGeometry(1, 12, 8);
            cylinder = Scene.CreateCylinderGeometry(.5f, .5f, 1, 8);
            cone = Scene.CreateConeGeometry(1, 1, 6);
            Scene.Environment = SceneEnvironment.Default with
            {
                Background = new(.59f, .79f, .86f, 1),
                AmbientColor = new(.58f, .74f, .8f),
                AmbientIntensity = .7f,
                FogColor = new(.55f, .74f, .78f),
                FogDensity = .006f,
                Sky = SkyMode.Procedural,
                SunDirection = new(.4f, .6f, -.4f),
                SkyHaze = .4f,
                SkyClouds = .3f
            };
            // The original RGBE source is encoded into filterable sRGB at 1/16 exposure.
            // Restoring x16 radiance preserves HDR highlights without ThreeNet 0.12's float-mipmap panic.
            var environmentPath = Path.Combine(AppContext.BaseDirectory, "assets/textures/nusantara-sky-scaled.png");
            if (File.Exists(environmentPath)) Scene.Environment = Scene.Environment with { EnvironmentMap = Scene.LoadTexture(environmentPath), EnvironmentIntensity = 16 * .4f };
            sun = Scene.AddLight(Light.Directional(new(1, .89f, .7f), 3));
            sun.Position = new(25, 40, 20);
            sun.LookAt(Vector3.Zero);
            Scene.AddLight(Light.Hemisphere(new(.65f, .85f, 1), new(.25f, .4f, .4f), .2f));
            Camera = Scene.AddCamera(ThreeNet.Camera.Perspective(50 * MathF.PI / 180));
            water = new WaterSurface(Scene, Vector3.Zero, 170, -3, 128, 128)
            {
                Style = WaterStyle.Pool with
                {
                    ShallowColor = new(.04f, .39f, .38f),
                    DeepColor = new(.006f, .075f, .12f),
                    Absorption = WaterClarity,
                    // Steep ripples (wakes, splashes) whiten; calm water stays clear.
                    Foam = .3f,
                    Detail = WaveStrength,
                    DetailSpeed = .35f,
                    Displacement = .32f,
                    Caustics = .55f
                }
            };
            water.Material.Update(x => x with { Shader = WaterOptics.Create(Scene, game.World), Transmission = .16f, Thickness = .2f, Ior = 1.333f });
            var sandTexture = Scene.LoadTextureCached(Path.Combine(AppContext.BaseDirectory, "assets/textures/coral-sand.png"));
            sandTexture.SetSampler(WrapMode.Repeat, WrapMode.Repeat, true, true);
            var sand = water.AddFloor(MaterialOptions.Pbr(new(.75f, .75f, .70f, 1), 0, .95f) with { BaseColorMap = sandTexture });
            terrain = new TerrainStreamer(Scene, game.World, sand);
            terrain.Update(Traversal.Position(game.Player));
            var seed = Array.FindIndex(Catalog.Regions, x => x.Id == game.Region.Id);
            var random = new Random(431 + seed * 7);
            BuildLandmarks();
            UnderwaterGarden(random);
            Dock();
            Boat = Scene.CreateNode(name: "player-boat");
            BuildBoat();
            BuildPlayer();
            BuildExhibition();
            BuildBoss();
            BuildLivingWorld();
            BuildWildlife();
            IslandCascade();
            bobber = Mesh(sphere, "#F3AC66", new(0, .15f, -2), new(.1f, .18f, .1f));
            fishingLine = Mesh(cylinder, "#DDECE6", new(0, 1, -2), new(.012f, 2, .012f));
            var fishPath = Path.Combine(AppContext.BaseDirectory, "assets/models/tuna.glb");
            if (File.Exists(fishPath))
            {
                try
                {
                    var oldClips = Scene.Animations.Select(x => x.Id).ToHashSet();
                    var model = Scene.LoadGltf(fishPath);
                    model.Root.Scale = new(.38f);
                    fish.Add(model.Root);
                    foreach (var clip in Scene.Animations.Where(x => !oldClips.Contains(x.Id))) clip.Play();
                }
                catch (ThreeNetException ex) { AssetWarning = ex.Message; }
            }
            for (var i = 0; i < 12; i++)
            {
                var swimmingFish = Scene.CreateNode(name: "reef-fish");
                Asset("reef-fish", Vector3.Zero, new(.7f + i % 3 * .1f), swimmingFish, false);
                fish.Add(swimmingFish);
            }
            rain = Mesh(cylinder, "#B7D8DB", Vector3.Zero, new(.018f, .45f, .018f));
            this.sceneCache.SetInstances(rain, Enumerable.Range(0, 110).Select(_ => Matrix4x4.CreateTranslation(random.Next(-20, 20), random.Next(1, 15), random.Next(-20, 20))).ToArray());
            rain.Visible = false;
            view.Scene = Scene;
            view.Camera = Camera;
            orbit = new OrbitController(view, Camera)
            {
                Target = new(0, 1, 2),
                Distance = 18,
                MinDistance = 7,
                MaxDistance = 55,
                Yaw = .6f,
                Pitch = .28f,
                EnablePan = false
            };
            orbit.Apply();
            BatchSwayingVegetation();
            if(!Program.Arguments.Contains("--no-batching")) BatchStaticScenery();
        }
        catch
        {
            Scene.Dispose();
            throw;
        }
    }

    private Material Mat(string color)
    {
        if (materials.TryGetValue(color, out var material)) return material;
        var c = global::Avalonia.Media.Color.Parse(color);
        // Hex palette values are sRGB; PBR factors are linear, like the imported glTF assets.
        static float Linear(byte value)
        {
            var srgb = value / 255f;
            return srgb <= .04045f ? srgb / 12.92f : MathF.Pow((srgb + .055f) / 1.055f, 2.4f);
        }
        material = Scene.CreateMaterial(MaterialOptions.Pbr(new(Linear(c.R), Linear(c.G), Linear(c.B), 1), 0, .8f));
        materials.Add(color, material);
        return material;
    }
    private Node Mesh(Geometry geometry, string color, Vector3 position, Vector3 scale, Node? parent = null)
    {
        var node = Scene.AddMesh(geometry, Mat(color), parent);
        node.Position = position;
        node.Scale = scale;
        if(parent is null) staticMeshes.Add(node);
        return node;
    }
    private void Palm(Vector3 position, float size)
    {
        if (Asset("coconut-palm", position, new(size)) is { } palm)
        {
            palm.EulerAngles = new(0, position.X * .71f + position.Z * .37f, 0);
            // Tall trunks bend less than saplings and sway more slowly.
            Sway(palm, position, size < .5f ? .07f : .028f, size < .5f ? 1.4f : .75f);
            return;
        }
        Mesh(cylinder, "#90734D", position + new Vector3(0, 1.6f * size, 0), new(.24f, 3.2f * size, .24f));
        for (var i = 0; i < 7; i++)
        {
            var angle = i / 7f * MathF.Tau;
            Mesh(sphere, "#3D9670", position + new Vector3(MathF.Cos(angle) * .8f, 3 * size, MathF.Sin(angle) * .8f), new(1.5f * size, .12f, .35f)).EulerAngles = new(0, -angle, .12f);
        }
    }
    private void Dock()
    {
        if (Asset("fishing-pier", Vector3.Zero, Vector3.One) is not null)
        {
            Mesh(cylinder, "#DAC58B", new(-5.3f, 3.4f, -6), new(.07f, 4, .07f));
            Mesh(box, "#D45D4B", new(-4.7f, 5, -6), new(1.2f, .4f, .04f));
            Mesh(box, "#F5EDD5", new(-4.7f, 4.6f, -6), new(1.2f, .4f, .04f));
            return;
        }
        for (var i = 0; i < 16; i++) Mesh(box, i % 2 == 0 ? "#A17E53" : "#B38B58", new(-7, .55f, i * .65f - 8), new(3, .18f, .59f));
        foreach (var x in new[] { -8.2f, -5.8f })
            foreach (var z in new[] { -8f, -4f, 0f, 2f }) Mesh(cylinder, "#685B48", new(x, -.1f, z), new(.23f, 2, .23f));
        Mesh(box, "#B88E5A", new(-7, 2, -6), new(3.6f, 2.8f, 3));
        Mesh(cone, "#724F3C", new(-7, 3.7f, -6), new(3.3f, 1.5f, 3));
        Mesh(box, "#3C6970", new(-7, 1.9f, -4.46f), new(.9f, 1.8f, .05f));
        Mesh(cylinder, "#DAC58B", new(-5.3f, 3.4f, -6), new(.07f, 4, .07f));
        Mesh(box, "#D45D4B", new(-4.7f, 5, -6), new(1.2f, .4f, .04f));
        Mesh(box, "#F5EDD5", new(-4.7f, 4.6f, -6), new(1.2f, .4f, .04f));
    }
    private void ProceduralBoat()
    {
        Mesh(sphere, "#A96F43", new(0, 0, 0), new(1, .4f, 2.5f), Boat);
        Mesh(box, "#357F81", new(0, .28f, 0), new(1.5f, .16f, 3.3f), Boat);
        foreach (var x in new[] { -1.6f, 1.6f }) Mesh(cylinder, "#D7C08A", new(x, -.12f, 0), new(.22f, 3.8f, .22f), Boat).EulerAngles = new(MathF.PI / 2, 0, 0);
        foreach (var z in new[] { -1f, 1f }) Mesh(box, "#D7C08A", new(0, .1f, z), new(3.5f, .1f, .14f), Boat);
    }
    private void UnderwaterGarden(Random random)
    {
        // Objects sit below the actual transparent surface; caustic shader shares its wave field.
        for (var i = 0; i < 42; i++)
        {
            var x = random.Next(-15, 16);
            var z = random.Next(-10, 16);
            var bed = -game.World.DepthAt(x, z);
            var rock = Asset("karst", new(x, bed, z), new(.8f + (float)random.NextDouble(), .5f, 1), smallRock: true);
            if (rock is not null) rock.EulerAngles = new(0, i * 2.4f, 0);
            if (i % 3 == 0)
            {
                Asset("branching-coral", new(x, bed + .3f, z), new(.7f + i % 2 * .2f));
            }
            else
            {
                for (var blade = 0; blade < 3; blade++)
                {
                    var seagrass = Mesh(sphere, "#457F58", new(x + blade * .17f, bed + .6f, z), new(.06f, .65f, .18f));
                    seagrass.EulerAngles = new(0, blade, .2f);
                    Sway(seagrass, new(x + blade * .17f, bed, z), .16f, .5f, underwater: true);
                }
            }
        }
    }
    public void Update(float dt, float time, Vector2 move)
    {
        dt = Math.Clamp(dt, 0, .1f);
        lastDt = dt;
        var before = boatPosition;
        boatPosition = new(game.Player.BoatX, .2f, game.Player.BoatZ);
        terrain.Update(Traversal.Position(game.Player));
        UpdatePlayerBoat(before, dt, time);
        var playerPosition = new Vector3(game.Player.PositionX, game.Player.OnFoot ? .8f : .2f, game.Player.PositionZ);
        orbit.Target = reviewTarget ?? playerPosition + new Vector3(0, .8f, -2);
        orbit.Apply();
        if (game.Player.InWater && reviewTarget is null)
        {
            Camera.Position = new(game.Player.PositionX, -.35f, game.Player.PositionZ - 1.3f);
            Camera.LookAt(new(game.Player.PositionX, -.65f, game.Player.PositionZ - 6));
        }
        var floatPosition = new Vector3(game.HookPosition.X, MathF.Sin(time * 3) * .04f, game.HookPosition.Y);
        bobber.Position = floatPosition;
        if (previousPhase != game.Phase && game.Phase is FishingPhase.Waiting or FishingPhase.Fighting)
            water.Splash(floatPosition, .8f, .18f);
        previousPhase = game.Phase;
        bobber.Visible = game.Busy && game.Player.CatchingMethod == CatchingMethod.Rod;
        fishingLine.Visible = game.Busy && game.Player.CatchingMethod == CatchingMethod.Rod;
        var lineStart = playerPosition + new Vector3(.3f, 2, -1.8f);
        var delta = floatPosition - lineStart;
        fishingLine.Position = (lineStart + floatPosition) / 2;
        fishingLine.Scale = new(.012f, delta.Length(), .012f);
        var normalized = Vector3.Normalize(delta);
        fishingLine.Rotation = Quaternion.CreateFromAxisAngle(Vector3.Normalize(Vector3.Cross(Vector3.UnitY, normalized)), MathF.Acos(Vector3.Dot(Vector3.UnitY, normalized)));
        for (var i = 0; i < fish.Count; i++)
        {
            var angle = time * .3f + i * .48f;
            fish[i].Position = new(MathF.Cos(angle) * (4 + i * .4f), -.7f - (i % 3) * .35f, MathF.Sin(angle) * (3 + i * .3f) + 3);
            fish[i].EulerAngles = new(0, -angle - MathF.PI / 2, 0);
        }
        Scene.UpdateAnimations(dt);
        water.Style = water.Style with { Absorption = WaterClarity, Detail = WaveStrength };
        water.Simulation.RainRate = game.Weather is Weather.Rain or Weather.Storm or Weather.Lightning ? 12 : 0;
        water.Update(dt);
        if (!ReducedMotion) ambientTime += dt;
        UpdateAtmosphere(dt, ambientTime);
        UpdateLivingWorld(dt, ambientTime);
        UpdateNature(dt, ambientTime);
        UpdatePlayerVisual(move, time);
        UpdateExhibition(time);
        UpdateCatchVisual(time);
        UpdateBoss(time);
        UpdatePeers(dt,time);
        rain.Visible = game.Weather is Weather.Rain or Weather.Storm or Weather.Lightning;
        rain.Position = boatPosition + new Vector3(0, -(time * 8 % 12), 0);
    }
    /// <summary>Swell height in metres for the current settings, weather and body of water.</summary>
    private float SwellAmplitude()
    {
        var weather = game.Weather switch { Weather.Storm or Weather.Lightning => 2.2f, Weather.Rain => 1.4f, Weather.Fog => .8f, _ => 1 };
        var basin = game.World.Biome switch
        {
            Biome.Lake or Biome.ThreeLakes or Biome.Pothole or Biome.Quarry or Biome.Cave or Biome.Crystal => .45f,
            Biome.River or Biome.City => .6f,
            _ => 1
        };
        return (.07f + WaveStrength * 1.5f) * weather * basin * (ReducedMotion ? .2f : 1);
    }

    private static float TurnToward(float from, float to, float amount)
    {
        var delta = MathF.IEEERemainder(to - from, MathF.Tau);
        return from + delta * amount;
    }

    private void UpdatePlayerBoat(Vector3 before, float dt, float time)
    {
        var hull = BoatHull.For(game.Player.BoatIndex);
        boatFloat.HalfLength = hull.HalfLength; boatFloat.HalfWidth = hull.HalfWidth;
        boatWake.HalfLength = hull.HalfLength * .85f; boatWake.HalfWidth = hull.HalfWidth * .8f;
        var previousYaw = boatYaw;
        // The simulation snaps the heading to the helm; the hull swings round over a few frames.
        boatYaw = float.IsNaN(boatYaw) ? game.Player.BoatHeading : TurnToward(boatYaw, game.Player.BoatHeading, 1 - MathF.Exp(-dt * 6));
        var turnRate = dt > 0 && !float.IsNaN(previousYaw) ? MathF.IEEERemainder(boatYaw - previousYaw, MathF.Tau) / dt : 0;
        var travelled = new Vector2(boatPosition.X - before.X, boatPosition.Z - before.Z).Length();
        var speed = dt > 0 && travelled < 6 ? travelled / dt : 0;
        // Planing lifts the bow a little; turning leans the hull into the turn.
        var trim = Math.Clamp(speed / 7, 0, 1) * .06f;
        var bank = Math.Clamp(-turnRate * .05f, -.12f, .12f);
        boatFloat.Apply(Boat, water, boatPosition, boatYaw, time, dt, SwellAmplitude(), trim, bank);
        boatWake.Update(water, boatPosition, dt);
    }

    public void Dispose()
    {
        orbit.Dispose();
        view.IsRendering = false;
        view.Scene = null;
        view.Camera = null;
        terrain.Dispose();
        Scene.Dispose();
    }
    public void AdjustCamera(Vector2 input, float zoom)
    {
        orbit.Yaw += input.X * 2;
        orbit.Pitch = Math.Clamp(orbit.Pitch - input.Y * 2, .08f, 1.3f);
        orbit.Distance = Math.Clamp(orbit.Distance + zoom * .2f, orbit.MinDistance, orbit.MaxDistance);
    }
    private Vector3? reviewTarget;
    internal void ReviewCamera(Vector3? target, float distance = 18, float yaw = .6f, float pitch = .28f)
    {
        reviewTarget = target;
        orbit.Distance = distance; orbit.Yaw = yaw; orbit.Pitch = pitch;
    }
}
