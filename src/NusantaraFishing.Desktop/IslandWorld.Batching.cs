using System.Numerics;
using ThreeNet;

namespace NusantaraFishing.Desktop;

public sealed partial class IslandWorld
{
    private readonly List<Node> staticMeshes=[];
    public int StaticInstances { get; private set; }
    public int StaticBatches { get; private set; }
    private void BatchStaticScenery()
    {
        var dynamicIds=waterfalls.Concat(vents).Select(x=>x.Id).Concat(new[]{bobber.Id,fishingLine.Id,rain.Id}).ToHashSet();
        var groups=staticMeshes.Where(x=>!dynamicIds.Contains(x.Id) && x.Material is not null && x.Geometry is not null)
            .GroupBy(x=>(Geometry:x.Geometry!.Id,Material:x.Material!.Id,Cast:x.CastShadow,Receive:x.ReceiveShadow)).ToArray();
        foreach(var group in groups)
        {
            var originals=group.ToArray();
            if(originals.Length<2) continue;
            var first=originals[0];
            var transforms=originals.Select(x=>x.WorldMatrix).ToArray();
            var batch=Scene.AddMesh(first.Geometry!,first.Material!,name:"static-instance-batch");
            batch.CastShadow=group.Key.Cast; batch.ReceiveShadow=group.Key.Receive;
            sceneCache.SetInstances(batch, transforms);
            foreach(var original in originals) Scene.Remove(original);
            StaticInstances+=transforms.Length; StaticBatches++;
        }
        staticMeshes.Clear();
    }
}
