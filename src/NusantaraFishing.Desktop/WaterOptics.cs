using System.Globalization;
using System.Text;
using NusantaraFishing.Core;
using ThreeNet;
using ThreeNet.Effects;

namespace NusantaraFishing.Desktop;

public static class WaterOptics
{
    /// <summary>Specializes the documented public ThreeNet effect source for the authored seabed.</summary>
    public static Shader Create(Scene scene, WorldDefinition world)
    {
        var expression = world.Biome switch
        {
            Biome.River => "1.5 + 2.5 * exp(-p.x*p.x/150.0)",
            Biome.City => "1.5 + 0.35 * sin(p.y/8.0)",
            Biome.Pothole => "0.8 + 1.7 * exp(-dot(p,p)/400.0)",
            Biome.Quarry => "2.0 + floor(min(length(p)/10.0,5.0))",
            Biome.Lake or Biome.ThreeLakes => "2.0 + min(length(p)/10.0,7.0)",
            Biome.Cave or Biome.Crystal => "2.0 + abs(sin(p.x/9.0)*cos(p.y/11.0))*5.0",
            Biome.Volcano => "2.0 + min(length(p)/8.0,8.0)",
            Biome.Dimension => "2.5 + abs(sin(p.x/7.0+p.y/6.0))*4.0",
            _ => "1.8 + min(length(p)/18.0,5.0)"
        };
        var depth = new StringBuilder("fn nusantara_depth(p: vec2<f32>) -> f32 { var depth = " + expression + ";\n");
        static string N(float x) => x.ToString("0.0###", CultureInfo.InvariantCulture);
        foreach (var reef in world.Landmarks.Where(x => x.Kind == LandmarkKind.Reef))
        {
            depth.Append("{ let q=(p-vec2<f32>(").Append(N(reef.X)).Append(',').Append(N(reef.Z))
                .Append("))/vec2<f32>(").Append(N(reef.Width)).Append(',').Append(N(reef.Depth))
                .Append("); depth -= exp(-dot(q,q)*4.0)*0.8; }\n");
        }
        depth.Append("return clamp(depth,0.6,12.0); }\n");
        const string marker = "let depth = max(level - context.custom2.w, 0.0);";
        if (!EffectShaders.Water.Contains(marker, StringComparison.Ordinal))
            throw new InvalidOperationException("ThreeNet water effect changed; update the optical-depth adapter.");
        var source = depth + EffectShaders.Water.Replace(marker,
            "let depth = max(nusantara_depth(context.world_position.xz) + level, 0.0);", StringComparison.Ordinal)
            .Replace("out.reflectance = 0.95;", "out.reflectance = 0.32;", StringComparison.Ordinal)
            .Replace("out.roughness = mix(0.035, 0.55, foam);", "out.roughness = mix(0.18, 0.55, foam);", StringComparison.Ordinal)
            .Replace("let detail_strength = context.custom3.y;", "let detail_strength = context.custom3.y / (1.0 + length(dpdx(context.world_position.xz)) * 3.0);", StringComparison.Ordinal)
            .Replace("detail_strength * 14.0", "detail_strength * 6.0", StringComparison.Ordinal)
            .Replace("* 1.7", "* 0.65", StringComparison.Ordinal)
            .Replace("* 3.9", "* 1.5", StringComparison.Ordinal)
            .Replace("let absorption = context.custom1.w;", "let absorption = context.custom1.w * 1.6;", StringComparison.Ordinal);
        var shader = scene.CreateShader(source, name: "nusantara.depth-aware-water");
        // ThreeNet 0.12 keys compiled shaders by ID/version rather than scene identity.
        // Each authored region therefore needs a distinct version for its depth function.
        var regionIndex = Array.FindIndex(Catalog.Regions, x => x.Id == world.RegionId);
        for (var i = 0; i <= regionIndex; i++) shader.Update(source);
        return shader;
    }
}
