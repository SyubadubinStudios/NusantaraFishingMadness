namespace NusantaraFishing.Desktop;

/// <summary>World-space limestone and flowing water; retain the renderer's PBR lighting and fog.</summary>
internal static class NatureShaders
{
    private const string Noise = """
        fn nature_hash(p: vec3<f32>) -> f32 {
            let q = fract(p * 0.1031);
            let r = q + dot(q, q.yzx + vec3<f32>(33.33));
            return fract((r.x + r.y) * r.z);
        }
        fn nature_noise(p: vec3<f32>) -> f32 {
            let i = floor(p); let f = fract(p); let u = f*f*(3.0-2.0*f);
            return mix(mix(mix(nature_hash(i), nature_hash(i+vec3<f32>(1,0,0)), u.x),
                           mix(nature_hash(i+vec3<f32>(0,1,0)), nature_hash(i+vec3<f32>(1,1,0)), u.x), u.y),
                       mix(mix(nature_hash(i+vec3<f32>(0,0,1)), nature_hash(i+vec3<f32>(1,0,1)), u.x),
                           mix(nature_hash(i+vec3<f32>(0,1,1)), nature_hash(i+vec3<f32>(1,1,1)), u.x), u.y), u.z);
        }
        """;

    // custom0.xyz = foliage tint (linear); w = foliage amount. custom1.xyz = sand tint.
    public const string Limestone = Noise + """
        fn user_surface(context: SurfaceContext, surface: Surface) -> Surface {
            var out = surface;
            let p = context.world_position;
            let broad = nature_noise(p * 0.23);
            let detail = nature_noise(p * 1.7);
            let strata = sin(p.y*2.2 + broad*9.0 + sin(p.x*0.4+p.z*0.3));
            let vein = smoothstep(0.88, 0.99, strata) * 0.012;
            let stone = mix(vec3<f32>(0.19,0.22,0.20), vec3<f32>(0.46,0.45,0.36), broad);
            let rock = stone * (0.85 + detail*0.24) - vec3<f32>(vein);
            let slope = abs(context.world_normal.y);
            let moss = smoothstep(0.30, 0.86, slope + (broad-0.5)*0.45)
                * smoothstep(0.65, 2.5, p.y) * context.custom0.w;
            let green = mix(vec3<f32>(0.045,0.065,0.024), context.custom0.xyz, 0.35)
                * (0.65 + broad*0.65 + detail*0.2);
            let dry = mix(rock, green, moss);
            let beach = (1.0-smoothstep(0.3, 1.1, p.y)) * smoothstep(0.4, 0.85, slope);
            let wet = mix(0.52, 1.0, smoothstep(-0.3, 1.0, p.y));
            out.albedo = mix(dry, context.custom1.xyz*(0.85+detail*0.25), beach) * wet;
            out.roughness = mix(0.48, 0.91, smoothstep(0.0, 1.0, p.y));
            out.reflectance = 0.22;
            // Screen-space surface gradient adds small erosion detail without another texture or mesh.
            let grain = detail*0.055 + sin(p.x*29.0+p.z*21.0)*sin(p.y*27.0-p.z*17.0)*0.004;
            let dx = dpdx(p); let dy = dpdy(p);
            let n = normalize(context.world_normal);
            let rx = cross(dy,n); let ry = cross(n,dx);
            let determinant = dot(dx,rx);
            let gradient = (dpdx(grain)*rx + dpdy(grain)*ry)*sign(determinant);
            out.normal = normalize(n - gradient/max(abs(determinant),0.00001));
            return out;
        }
        """;

    // custom0.x = simulation time, supplied by the same reduced-motion clock as vegetation.
    public const string Cascade = Noise + """
        fn user_surface(context: SurfaceContext, surface: Surface) -> Surface {
            var out = surface;
            let uv = context.uv;
            let t = context.custom0.x;
            let x = uv.x;
            let fall = uv.y;
            let turbulence = nature_noise(vec3<f32>(x*18.0, fall*7.0-t*1.8, 0.7));
            let coarse = nature_noise(vec3<f32>(x*8.0, fall*2.5-t*2.2, 3.1));
            let warped = x + (coarse-0.5)*0.14 + (turbulence-0.5)*0.035;
            let strands = 0.5+0.5*sin(warped*210.0+turbulence*7.0+sin(fall*11.0-t*4.0));
            let fine = 0.5+0.5*sin(warped*510.0+fall*15.0-t*9.0);
            let rim = smoothstep(0.0, 0.12, x) * (1.0-smoothstep(0.88, 1.0, x));
            let ends = smoothstep(0.0, 0.055, fall) * (1.0-smoothstep(0.88, 1.0, fall));
            let curtain = smoothstep(0.25, 0.86, strands*0.7+turbulence*0.3);
            let aerated = smoothstep(0.15, 0.85, fall) * 0.28 + curtain*0.42 + turbulence*0.16;
            out.albedo = mix(vec3<f32>(0.26,0.49,0.51), vec3<f32>(0.9,0.96,0.94), aerated);
            out.alpha = rim * ends * (0.055 + curtain*0.47 + fine*0.055 + turbulence*0.12);
            out.roughness = 0.28 + aerated*0.4;
            out.reflectance = 0.3;
            return out;
        }
        """;
}
