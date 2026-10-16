using System.Numerics;
using NusantaraFishing.Core;
using ThreeNet;
using ThreeNet.Interop;

namespace NusantaraFishing.Desktop;

/// <summary>Loads a bounded neighborhood of seabed tiles; destroys evicted geometry immediately.</summary>
public sealed class TerrainStreamer(Scene scene, WorldDefinition world, Material material) : IDisposable
{
    private readonly Dictionary<(int X, int Z), (Node Node, Geometry Geometry)> chunks = [];
    private (int X, int Z)? center;
    public int ResidentChunks => chunks.Count;
    public const int ChunkSize = 16;
    public void Update(Vector2 position)
    {
        var next = ((int)MathF.Floor(position.X / ChunkSize), (int)MathF.Floor(position.Y / ChunkSize));
        if (center == next) return;
        center = next;
        var desired = new HashSet<(int X, int Z)>();
        for (var z = next.Item2 - 3; z <= next.Item2 + 3; z++)
            for (var x = next.Item1 - 3; x <= next.Item1 + 3; x++)
                if (x is >= -5 and <= 4 && z is >= -5 and <= 4) desired.Add((x, z));
        foreach (var key in chunks.Keys.Where(x => !desired.Contains(x)).ToArray())
        {
            var chunk = chunks[key]; scene.Remove(chunk.Node); chunk.Geometry.Destroy(); chunks.Remove(key);
        }
        foreach (var key in desired)
        {
            if (chunks.ContainsKey(key)) continue;
            var geometry = Build(key.X, key.Z);
            chunks[key] = (scene.AddMesh(geometry, material, name: $"terrain-{key.X}-{key.Z}"), geometry);
        }
    }
    private Geometry Build(int chunkX, int chunkZ)
    {
        const int segments = 8, stride = segments + 1;
        var vertices = new Vertex[stride * stride];
        var indices = new uint[segments * segments * 6];
        for (var z = 0; z < stride; z++)
            for (var x = 0; x < stride; x++)
            {
                var px = chunkX * ChunkSize + x * 2f;
                var pz = chunkZ * ChunkSize + z * 2f;
                const float e = .2f;
                var normal = Vector3.Normalize(new Vector3(world.DepthAt(px + e, pz) - world.DepthAt(px - e, pz), 2 * e,
                    world.DepthAt(px, pz + e) - world.DepthAt(px, pz - e)));
                vertices[z * stride + x] = new(new(px, -world.DepthAt(px, pz), pz), normal, new(px * .1f, pz * .1f));
            }
        var i = 0;
        for (var z = 0; z < segments; z++)
            for (var x = 0; x < segments; x++)
            {
                var a = (uint)(z * stride + x); var b = a + 1; var c = a + stride; var d = c + 1;
                indices[i++] = a; indices[i++] = c; indices[i++] = b;
                indices[i++] = b; indices[i++] = c; indices[i++] = d;
            }
        return scene.CreateGeometry(vertices, indices);
    }
    public void Dispose()
    {
        foreach (var item in chunks.Values) { scene.Remove(item.Node); item.Geometry.Destroy(); }
        chunks.Clear();
    }
}
