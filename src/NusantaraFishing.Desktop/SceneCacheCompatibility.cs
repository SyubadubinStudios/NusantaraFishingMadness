using System.Numerics;
using ThreeNet;

namespace NusantaraFishing.Desktop;

/// <summary>ThreeNet 0.12 GPU caches use scene-local resource IDs. Keep scene swaps isolated.</summary>
internal sealed class SceneCacheCompatibility
{
    private readonly Dictionary<uint, int> instanceWrites = [];

    public static void FlushResources(Renderer? renderer)
    {
        if (renderer is null) return;
        // Rendering an empty scene prunes geometry/material/texture caches and resets the
        // environment binding without destroying the device or its built-in pipelines.
        using var empty = new Scene();
        var camera = empty.AddCamera(Camera.Perspective(MathF.PI / 3));
        camera.Position = new(0, 0, 5);
        renderer.Render(empty, camera);
    }

    public void SetInstances(Node node, ReadOnlySpan<Matrix4x4> matrices)
    {
        // Instance buffers are not pruned by 0.12. Each reused node ID must have a newer
        // edit version, including nodes that were offscreen when the last scene ended.
        var writes = instanceWrites.GetValueOrDefault(node.Id) + 1;
        for (var i = 0; i < writes; i++) node.SetInstances(matrices);
        instanceWrites[node.Id] = writes;
    }
}
