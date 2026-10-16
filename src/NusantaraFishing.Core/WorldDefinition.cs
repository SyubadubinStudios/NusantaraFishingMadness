using System.Numerics;

namespace NusantaraFishing.Core;

public enum Biome { Coast, Reef, Lake, ThreeLakes, River, City, Pothole, Waterfall, Cave, Quarry, Sky, Volcano, Crystal, Dimension }
public enum LandmarkKind { Island, Bank, Building, Road, Bridge, Cliff, Arch, Crystal, Waterfall, Vent, Cloud, Reef, Sign }
public sealed record Landmark(LandmarkKind Kind, float X, float Z, float Width, float Height, float Depth, string Color, bool BlocksBoat = true)
{
    public bool Contains(Vector2 point, float padding = 0) =>
        Math.Abs(point.X - X) < Width / 2 + padding && Math.Abs(point.Y - Z) < Depth / 2 + padding;

    /// <summary>
    /// Island hill surface shared by rendering and climbing: a rounded crown, steep forested flanks
    /// cut by gullies and a beach. <paramref name="r"/> is the radius relative to the irregular rim.
    /// </summary>
    public float IslandSurface(float x, float z, out float r)
    {
        var dx = (x - X) / (Width * .5f);
        var dz = (z - Z) / (Depth * .5f);
        var angle = MathF.Atan2(dz, dx);
        var rim = 1 + .06f * MathF.Sin(angle * 5 + X) + .03f * MathF.Sin(angle * 11 + Z);
        r = MathF.Sqrt(dx * dx + dz * dz) / rim;
        var hill = 0f;
        if (r < 1)
        {
            // Broad shoulders, asymmetric saddles and erosion break up the old uniform dome.
            // All detail vanishes at the rim, preserving the existing navigable waterline.
            var shoulder = MathF.Pow(1 - MathF.Pow(r, 3.2f), .8f);
            var ridge = .86f + .12f * MathF.Sin(dx * 4.4f + X * .31f) * MathF.Cos(dz * 3.7f + Z * .23f)
                + .08f * MathF.Sin(dx * 7.3f + dz * 3.1f + Z);
            hill = Height * shoulder * ridge;
            var gully = MathF.Sin(angle * 7 + X * .3f + r * 3);
            hill += Height * .07f * gully * r * (1 - r) * 4;
            hill += MathF.Sin(x * .9f + Z) * MathF.Cos(z * .7f + X) * Height * .016f * (1 - r);
        }
        var beach = float.Lerp(.5f, -.7f, Math.Clamp((r - .97f) / .15f, 0, 1));
        return Math.Max(hill, beach);
    }

    /// <summary>Waterline footprint of the rendered landmark, expanded by a hull radius.</summary>
    public bool BlocksSailing(Vector2 point, float radius)
    {
        if (!BlocksBoat) return false;
        var dx = point.X - X;
        var dz = point.Y - Z;
        bool Ellipse(float halfX, float halfZ) =>
            dx * dx / ((halfX + radius) * (halfX + radius)) + dz * dz / ((halfZ + radius) * (halfZ + radius)) < 1;
        switch (Kind)
        {
            case LandmarkKind.Island:
                // Beach dome and surrounding karst reach slightly past the authored rectangle.
                return Ellipse(Width * .58f, Depth * .58f);
            case LandmarkKind.Crystal:
                return Ellipse(Width * .95f, Depth * .95f);
            case LandmarkKind.Vent:
                return Ellipse(Width * .75f, Depth * .75f);
            case LandmarkKind.Sign:
                return dx * dx + dz * dz < (.25f + radius) * (.25f + radius);
            case LandmarkKind.Arch:
                // Boats pass beneath the span between the two pillars.
                return Contains(point, radius) && Math.Abs(dx) > Width * .28f - radius;
            case LandmarkKind.Cliff:
                // Karst blocks along a cliff overhang its ends by up to a third of one block.
                var count = Math.Clamp((int)(Math.Max(Width, Depth) / 8), 3, 10);
                var reach = Math.Max(.5f, .36f + .83f / count);
                return Width >= Depth
                    ? Math.Abs(dx) < Width * reach + radius && Math.Abs(dz) < Depth / 2 + radius
                    : Math.Abs(dx) < Width / 2 + radius && Math.Abs(dz) < Depth * reach + radius;
            case LandmarkKind.Building:
                // Raft houses are asymmetric (5/8 of the width on the -X side).
                return dx > -Width * .63f - radius && dx < Width * .5f + radius && Math.Abs(dz) < Depth / 2 + radius;
            default:
                return Contains(point, radius);
        }
    }
}
public sealed record Bait(string Id, string Name, string Description, int Cost, CatchKind? PreferredKind, float DepthBonus);

public sealed record WorldDefinition(string RegionId, Biome Biome, string SandColor, string VegetationColor,
    float Absorption, float CurrentX, float CurrentZ, string Mechanic, Landmark[] Landmarks)
{
    public const float Boundary = 72;
    public Vector2 Current => new(CurrentX, CurrentZ);
    public bool HasGovernmentPier => Biome is Biome.City or Biome.Quarry;
    public static readonly Landmark GovernmentPier = new(LandmarkKind.Bank, 16, 28, 8, 1.1f, 6, "#829590");
    public IEnumerable<Vector2> TreePositions()
    {
        foreach (var feature in Landmarks)
        {
            if (feature.Kind == LandmarkKind.Bank)
                for (var i = 0; i < 11; i++)
                    yield return new(feature.X - feature.Width * .4f + i * feature.Width * .08f, feature.Z + MathF.Sin(i * 2.1f) * feature.Depth * .22f);
            else if (feature.Kind == LandmarkKind.Island && Biome is Biome.Coast or Biome.Reef or Biome.Lake)
                for (var i = 0; i < 3; i++) yield return new(feature.X + i * 2 - 2, feature.Z);
        }
    }
    public bool Underground => Biome is Biome.Cave or Biome.Crystal;
    public static readonly Bait[] Baits =
    [
        new("worm", "Cacing", "Umpan umum, tanpa biaya.", 0, null, 0),
        new("shrimp", "Udang segar", "Peluang ikan dan krustasea meningkat.", 12, CatchKind.Fish, .1f),
        new("lure", "Lure bercahaya", "Menarik makhluk bayangan di perairan dalam.", 25, CatchKind.Shadow, .25f),
        new("crab", "Kepiting kecil", "Menarik gurita dan cumi.", 18, CatchKind.Cephalopod, .15f),
        new("relic", "Magnet peninggalan", "Meningkatkan peluang artefak dan barang unik.", 30, CatchKind.Artifact, .2f)
    ];

    public float DepthAt(float x, float z)
    {
        var distance = MathF.Sqrt(x * x + z * z);
        var depth = Biome switch
        {
            Biome.River => 1.5f + 2.5f * MathF.Exp(-x * x / 150),
            Biome.City => 1.5f + .35f * MathF.Sin(z / 8),
            Biome.Pothole => .8f + 1.7f * MathF.Exp(-distance * distance / 400),
            Biome.Quarry => 2 + MathF.Floor(Math.Min(distance / 10, 5)),
            Biome.Lake or Biome.ThreeLakes => 2 + Math.Min(distance / 10, 7),
            Biome.Cave or Biome.Crystal => 2 + MathF.Abs(MathF.Sin(x / 9) * MathF.Cos(z / 11)) * 5,
            Biome.Volcano => 2 + Math.Min(distance / 8, 8),
            Biome.Dimension => 2.5f + MathF.Abs(MathF.Sin(x / 7 + z / 6)) * 4,
            _ => 1.8f + Math.Min(distance / 18, 5)
        };
        // Smooth reef shelves rise from the bed without becoming invisible blockers.
        foreach (var reef in Landmarks)
        {
            if (reef.Kind != LandmarkKind.Reef) continue;
            var dx = (x - reef.X) / Math.Max(1, reef.Width);
            var dz = (z - reef.Z) / Math.Max(1, reef.Depth);
            depth -= MathF.Exp(-(dx * dx + dz * dz) * 4) * .8f;
        }
        return Math.Clamp(depth, .6f, 12);
    }

    public bool CanSail(Vector2 point, float radius = .7f)
    {
        if (Math.Abs(point.X) >= Boundary - radius || Math.Abs(point.Y) >= Boundary - radius) return false;
        if (HasGovernmentPier && GovernmentPier.Contains(point, radius)) return false;
        // Shared harbor: museum and aquarium pavilions, then the fishing pier jutting north.
        if (point.X > -25 - radius && point.X < -5 + radius && point.Y > -13 - radius && point.Y < -3 + radius) return false;
        if (point.X > -9 - radius && point.X < -5 + radius && point.Y > -8.5f - radius && point.Y < 2.2f + radius) return false;
        foreach (var landmark in Landmarks)
            if (landmark.BlocksSailing(point, radius)) return false;
        return true;
    }

    /// <summary>
    /// Hull footprint test: circles along the keel from midships to the bow. The stern is
    /// left out so a boat pressed against a wall can always turn away from it.
    /// </summary>
    public bool HullFits(Vector2 center, Vector2 forward, BoatHull hull)
    {
        if (!CanSail(center, hull.HalfWidth)) return false;
        var reach = hull.HalfLength - hull.HalfWidth;
        if (reach <= .05f || forward.LengthSquared() < .5f) return true;
        return CanSail(center + forward * reach * .5f, hull.HalfWidth) && CanSail(center + forward * reach, hull.HalfWidth);
    }

    public bool CanWalk(Vector2 point, bool climbing = false, bool governmentAccess = false)
    {
        if (HasGovernmentPier && GovernmentPier.Contains(point)) return governmentAccess;
        if (Math.Abs(point.X + 16) < .3f && point.Y is > -12.5f and < -4.6f) return false;
        // The common harbor connects the dock, museum, and aquarium.
        if ((point.X is > -25 and < -4 && point.Y is > -13 and < -3.5f) ||
            (point.X is > -8.5f and < -5.5f && point.Y is >= -4 and < 3)) return true;
        return Landmarks.Any(x => (x.Kind is LandmarkKind.Bank or LandmarkKind.Road or LandmarkKind.Bridge ||
            (climbing && x.Kind is LandmarkKind.Island or LandmarkKind.Cliff)) && x.Contains(point, -.4f));
    }

    public bool TryShorePoint(Vector2 boat, bool climbing, out Vector2 shore, bool governmentAccess = false)
    {
        if (HasGovernmentPier && governmentAccess)
        {
            var pier = GovernmentPier;
            var candidate = new Vector2(Math.Clamp(boat.X, pier.X - pier.Width / 2 + .6f, pier.X + pier.Width / 2 - .6f),
                Math.Clamp(boat.Y, pier.Z - pier.Depth / 2 + .6f, pier.Z + pier.Depth / 2 - .6f));
            if (Vector2.Distance(boat, candidate) <= 4.5f) { shore = candidate; return true; }
        }
        foreach (var feature in Landmarks)
        {
            if (feature.Kind is not (LandmarkKind.Bank or LandmarkKind.Road or LandmarkKind.Bridge) &&
                !(climbing && feature.Kind is LandmarkKind.Island or LandmarkKind.Cliff)) continue;
            var candidate = new Vector2(Math.Clamp(boat.X, feature.X - feature.Width / 2 + .6f, feature.X + feature.Width / 2 - .6f),
                Math.Clamp(boat.Y, feature.Z - feature.Depth / 2 + .6f, feature.Z + feature.Depth / 2 - .6f));
            if (Vector2.Distance(boat, candidate) <= 4.5f && CanWalk(candidate, climbing)) { shore = candidate; return true; }
        }
        shore = default; return false;
    }

    public float GroundHeight(Vector2 point)
    {
        if (HasGovernmentPier && GovernmentPier.Contains(point)) return GovernmentPier.Height;
        foreach (var feature in Landmarks)
        {
            if (!feature.Contains(point)) continue;
            if (feature.Kind is LandmarkKind.Bank or LandmarkKind.Bridge) return feature.Height + .35f;
            if (feature.Kind == LandmarkKind.Road) return feature.Height * .5f + .15f;
            if (feature.Kind == LandmarkKind.Island) return feature.IslandSurface(point.X, point.Y, out _);
            if (feature.Kind == LandmarkKind.Cliff)
            {
                var x = (point.X - feature.X) / Math.Max(1, feature.Width * .5f);
                var z = (point.Y - feature.Z) / Math.Max(1, feature.Depth * .5f);
                return feature.Height * .5f + MathF.Sqrt(Math.Max(0, 1 - x * x - z * z)) * feature.Height * .6f;
            }
        }
        return .65f;
    }

    public bool LineHitsReef(Vector2 start, Vector2 end)
    {
        foreach (var reef in Landmarks)
        {
            if (reef.Kind != LandmarkKind.Reef) continue;
            var center = new Vector2(reef.X, reef.Z);
            var delta = end - start;
            var t = delta.LengthSquared() < .001f ? 0 : Math.Clamp(Vector2.Dot(center - start, delta) / delta.LengthSquared(), 0, 1);
            if (Vector2.Distance(start + delta * t, center) < Math.Min(reef.Width, reef.Depth) / 2) return true;
        }
        return false;
    }
}

/// <summary>Authored landmark layouts: coordinates are intentional gameplay content, not random island seeds.</summary>
public static class Worlds
{
    private static Landmark L(LandmarkKind kind, float x, float z, float width, float height, float depth, string color, bool blocks = true)
        => new(kind, x, z, width, height, depth, color, blocks);
    public static readonly WorldDefinition[] All =
    [
        new("selatan", Biome.Coast, "#BAAE79", "#347A54", .08f, .12f, .02f,
            "Arus pantai mendorong perahu. Karang di timur dapat menggesek tali.",
            [L(LandmarkKind.Bank,-38,-27,58,3,23,"#D6C48F"), L(LandmarkKind.Cliff,12,-41,30,14,16,"#76866E"),
             L(LandmarkKind.Island,40,-25,17,9,14,"#387854"),L(LandmarkKind.Island,48,-48,12,16,10,"#46795C"),
             L(LandmarkKind.Reef,12,-7,5,.9f,7,"#9D7A70",false),L(LandmarkKind.Reef,20,8,6,1,5,"#BE9A73",false)]),
        new("ampat", Biome.Reef, "#D5C59B", "#246B48", .045f, -.04f, .06f,
            "Terumbu jernih kaya krustasea. Jaga arah kail agar tidak tersangkut.",
            [L(LandmarkKind.Island,-34,-30,16,19,13,"#34775D"),L(LandmarkKind.Island,-14,-39,13,23,11,"#2B6B4A"),
             L(LandmarkKind.Island,12,-28,12,17,14,"#377B59"),L(LandmarkKind.Island,36,-35,17,26,15,"#266948"),
             L(LandmarkKind.Arch,48,-12,15,12,6,"#7D8A71"),L(LandmarkKind.Reef,8,1,6,1,5,"#B68480",false),
             L(LandmarkKind.Reef,-4,-17,5,1,6,"#B4A467",false),L(LandmarkKind.Reef,20,15,7,1,5,"#B58D6A",false)]),
        new("toba", Biome.Lake, "#92977B", "#59734F", .12f, .02f, .02f,
            "Danau dalam: lure bercahaya dan kabut membantu menemukan penghuni langka.",
            [L(LandmarkKind.Cliff,-45,-25,20,17,70,"#607661"),L(LandmarkKind.Cliff,47,-24,18,20,65,"#597057"),
             L(LandmarkKind.Island,8,-36,32,7,23,"#65844D"),L(LandmarkKind.Building,8,-33,5,6,6,"#95604C"),
             L(LandmarkKind.Bank,0,-65,90,5,15,"#647E55")]),
        new("kelimutu", Biome.ThreeLakes, "#837E6A", "#68856D", .06f, 0, .03f,
            "Tiga cekungan berwarna: spesies mutan lebih sering muncul di kedalaman.",
            [L(LandmarkKind.Cliff,-42,-30,17,16,55,"#6D7770"),L(LandmarkKind.Cliff,42,-30,17,19,55,"#73806A"),
             L(LandmarkKind.Bank,0,-35,34,2,6,"#7C8276"),L(LandmarkKind.Bank,17,-55,5,3,40,"#8D8877"),
             L(LandmarkKind.Crystal,24,-44,8,5,8,"#62BCB5"),L(LandmarkKind.Vent,-20,-48,3,3,3,"#7EBBCE")]),
        new("kalimantan", Biome.River, "#8E8765", "#356942", .18f, .02f, .32f,
            "Arus sungai kuat. Bertahan dekat tepian mengurangi dorongan.",
            [L(LandmarkKind.Bank,-34,-12,21,3,110,"#587D43"),L(LandmarkKind.Bank,34,-12,23,4,110,"#52733F"),
             L(LandmarkKind.Building,-18,-23,7,5,6,"#A0764D"),L(LandmarkKind.Building,17,-8,7,4,6,"#B48A55"),
             L(LandmarkKind.Bridge,0,-40,53,7,3,"#9D8060",false),L(LandmarkKind.Reef,5,26,4,1,4,"#666C56",false)]),
        new("jakarta", Biome.City, "#978665", "#5F8163", .23f, .03f, .03f,
            "Banjir membawa barang unik. Magnet peninggalan meningkatkan peluang benda.",
            [L(LandmarkKind.Road,-40,-27,40,.8f,10,"#565D61"),L(LandmarkKind.Road,40,-27,40,.8f,10,"#565D61"),
             L(LandmarkKind.Bridge,0,-33,90,6,6,"#8B9496",false),
             L(LandmarkKind.Building,-35,-18,9,15,9,"#D4B896"),L(LandmarkKind.Building,-22,-20,9,22,10,"#93A5A8"),
             L(LandmarkKind.Building,20,-20,11,18,12,"#B6A28A"),L(LandmarkKind.Building,38,-25,12,27,12,"#7895A0"),
             L(LandmarkKind.Sign,8,-15,3,6,1,"#D29C55"),L(LandmarkKind.Building,30,16,13,9,10,"#C2A781")]),
        new("kubangan", Biome.Pothole, "#9F865E", "#728350", .2f, .01f, 0,
            "Kolam dangkal di jalan rusak. Sampah absurd dan lele mendominasi.",
            [L(LandmarkKind.Road,0,-23,95,1,12,"#55575A"),L(LandmarkKind.Road,28,17,30,1,60,"#57595D"),
             L(LandmarkKind.Road,-36,8,17,1,50,"#626365"),L(LandmarkKind.Sign,17,-14,2,4,1,"#E9B94B"),
             L(LandmarkKind.Building,-28,-36,9,4,7,"#A27A4F"),L(LandmarkKind.Bank,0,47,95,2,14,"#82935C")]),
        new("terjun", Biome.Waterfall, "#9FAD8A", "#377950", .055f, 0, .4f,
            "Air terjun menambah tekanan tali. Pindah ke kolam samping untuk arus tenang.",
            [L(LandmarkKind.Cliff,0,-39,95,24,15,"#607B69"),L(LandmarkKind.Waterfall,0,-29,15,23,2,"#A9DDE0"),
             L(LandmarkKind.Island,-32,-10,12,9,11,"#3E7C54"),L(LandmarkKind.Island,31,-7,13,10,12,"#3E7958"),
             L(LandmarkKind.Reef,8,-16,6,2,5,"#6E897B",false)]),
        new("gua", Biome.Cave, "#6E7488", "#626B83", .09f, .08f, .07f,
            "Gelap dan dalam. Lure bercahaya menarik Shadow Creature.",
            [L(LandmarkKind.Cliff,-42,0,18,22,120,"#505971"),L(LandmarkKind.Cliff,42,0,18,22,120,"#505971"),
             L(LandmarkKind.Arch,0,-24,58,24,8,"#69718A"),L(LandmarkKind.Arch,0,35,60,20,7,"#626B7E"),
             L(LandmarkKind.Crystal,-26,-15,3,8,3,"#82ACDB"),L(LandmarkKind.Crystal,27,17,4,10,3,"#88A1D0")]),
        new("tambang", Biome.Quarry, "#868C85", "#718879", .065f, 0, -.02f,
            "Teras tambang bertingkat. Mutasi meningkat di cekungan dalam.",
            [L(LandmarkKind.Cliff,-43,-12,15,8,100,"#8B958E"),L(LandmarkKind.Cliff,43,-12,15,11,100,"#7D8B87"),
             L(LandmarkKind.Cliff,0,-48,80,14,17,"#909B90"),L(LandmarkKind.Building,28,-31,8,6,9,"#BBA053"),
             L(LandmarkKind.Sign,10,-25,3,4,1,"#D3B855"),L(LandmarkKind.Vent,-21,22,4,2,4,"#88D998")]),
        new("awan", Biome.Sky, "#B5C3CF", "#82A49C", .035f, .18f, .14f,
            "Angin di atas awan menggerakkan perahu. Umpan udang mengundang ikan terbang.",
            [L(LandmarkKind.Cloud,-27,-20,24,6,18,"#D5E8ED",false),L(LandmarkKind.Cloud,27,-28,30,7,20,"#E2EDF2",false),
             L(LandmarkKind.Island,0,-42,20,5,18,"#8CB6A1"),L(LandmarkKind.Cloud,-40,25,30,8,22,"#C3DCE4",false),
             L(LandmarkKind.Arch,38,20,15,15,5,"#BDCADB")]),
        new("gunung", Biome.Volcano, "#766F68", "#85735D", .1f, -.12f, .08f,
            "Semburan panas terjadi berkala. Berhenti menarik saat geyser menyala.",
            [L(LandmarkKind.Cliff,-40,-20,18,20,95,"#756A62"),L(LandmarkKind.Cliff,40,-20,18,25,95,"#70645E"),
             L(LandmarkKind.Island,0,-45,35,27,25,"#75675D"),L(LandmarkKind.Vent,14,-10,4,4,4,"#E68A48"),
             L(LandmarkKind.Vent,-14,-23,4,6,4,"#DC7245"),L(LandmarkKind.Reef,22,19,6,2,6,"#86705D",false)]),
        new("kristal", Biome.Crystal, "#77778F", "#8B97AE", .05f, -.02f, .04f,
            "Kristal memantulkan cahaya. Shadow Creature mengikuti lure bercahaya.",
            [L(LandmarkKind.Cliff,-43,0,14,22,120,"#585D82"),L(LandmarkKind.Cliff,43,0,14,22,120,"#585D82"),
             L(LandmarkKind.Arch,0,-31,63,25,9,"#7C78A2"),L(LandmarkKind.Crystal,-26,-10,9,17,8,"#9B85DE"),
             L(LandmarkKind.Crystal,26,-20,8,23,7,"#78BED4"),L(LandmarkKind.Crystal,22,19,6,12,6,"#C19ADC")]),
        new("dimensi", Biome.Dimension, "#9181A3", "#8F79BA", .065f, .17f, -.12f,
            "Arus berputar dan makhluk legenda. Ikan Raja menunggu di perairan ini.",
            [L(LandmarkKind.Arch,0,-34,35,30,6,"#B096D3"),L(LandmarkKind.Cloud,-35,-15,20,9,17,"#AA9BBD",false),
             L(LandmarkKind.Island,35,-18,19,12,17,"#9980BB"),L(LandmarkKind.Crystal,-24,22,6,14,6,"#A6CAEB"),
             L(LandmarkKind.Vent,23,22,7,5,7,"#D9A5CC")])
    ];
    public static WorldDefinition For(string id) => All.First(x => x.RegionId == id);
}
