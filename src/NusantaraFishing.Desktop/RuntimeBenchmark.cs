using System.Diagnostics;
using System.Text.Json;
using ThreeNet.Avalonia;

namespace NusantaraFishing.Desktop;

public sealed class RuntimeBenchmark
{
    private sealed record Sample(double IntervalMs,float CpuMs,float GpuMs,int DrawCalls,int Triangles);
    private readonly List<Sample> samples=[];
    private int successfulFrames;
    private long allocatedAtStart;
    private double startedAt;
    private double latestFrame;
    public bool Ready=>samples.Count>=30 && latestFrame-startedAt>=15;
    public double FirstFrameSeconds { get; private set; }
    public void Record(ThreeNetView view,FrameEventArgs frame)
    {
        var stats=view.Stats;
        if(stats.DrawCalls==0) return;
        if(FirstFrameSeconds==0) FirstFrameSeconds=frame.TotalSeconds;
        if(++successfulFrames<=5)
        { Console.WriteLine($"BENCHMARK_WARMUP: {successfulFrames}, {frame.TotalSeconds:N1}s, {stats.DrawCalls} draws"); return; }
        if(samples.Count==0) { allocatedAtStart=GC.GetTotalAllocatedBytes(true); startedAt=frame.TotalSeconds; }
        samples.Add(new(frame.DeltaSeconds*1000,stats.CpuTimeMs,stats.GpuTimeMs,stats.DrawCalls,stats.Triangles));
        latestFrame=frame.TotalSeconds;
    }
    public string Write(ThreeNetView view,IslandWorld world,string output)
    {
        if(samples.Count<10) throw new InvalidOperationException("Benchmark needs at least ten steady frames.");
        var allocated=GC.GetTotalAllocatedBytes(true)-allocatedAtStart;
        var elapsed=samples.Sum(x=>x.IntervalMs)/1000;
        static double Percentile(IEnumerable<double> values,double p)
        { var a=values.Order().ToArray(); return a[(int)Math.Clamp(Math.Ceiling((a.Length-1)*p),0,a.Length-1)]; }
        var capture=Stopwatch.StartNew();
        for(var i=0;i<5;i++) using(view.CaptureFrame()) { }
        capture.Stop();
        var renderer=view.Renderer!;
        var assetFiles=Directory.GetFiles(Path.Combine(AppContext.BaseDirectory,"assets"),"*",SearchOption.AllDirectories);
        var report=new
        {
            RecordedAt=DateTimeOffset.UtcNow,OperatingSystem=Environment.OSVersion.ToString(),Runtime=Environment.Version.ToString(),
            CpuThreads=Environment.ProcessorCount,Adapter=renderer.AdapterName,Driver=renderer.AdapterDriver,Capabilities=renderer.Capabilities,
            Width=renderer.Width,Height=renderer.Height,RenderScale=view.RenderScale,FpsCap=view.MaxFramesPerSecond,
            ShadowMaps=view.RendererOptions.Shadows,MsaaSamples=view.RendererOptions.MsaaSamples,
            FrameLoop=Program.Arguments.Contains("--legacy-frame-loop")?"ThreeNet timer":"Coordinated start-to-start pacing",
            Samples=samples.Count,Seconds=elapsed,FirstRenderedFrameSeconds=FirstFrameSeconds,
            ActualFramesPerSecond=samples.Count/elapsed,
            FrameIntervalMedianMs=Percentile(samples.Select(x=>x.IntervalMs),.5),FrameIntervalP95Ms=Percentile(samples.Select(x=>x.IntervalMs),.95),
            NativeCpuMedianMs=Percentile(samples.Select(x=>(double)x.CpuMs),.5),NativeGpuMedianMs=Percentile(samples.Select(x=>(double)x.GpuMs),.5),
            MeanDrawCalls=samples.Average(x=>x.DrawCalls),MeanTriangles=samples.Average(x=>x.Triangles),
            ManagedAllocatedBytes=allocated,ManagedAllocatedBytesPerSecond=allocated/Math.Max(.001,elapsed),
            ProcessWorkingSetBytes=Environment.WorkingSet,ReadbackBytesPerFrame=renderer.Width*renderer.Height*4,
            MeanCaptureReadbackAndBitmapMs=capture.Elapsed.TotalMilliseconds/5,
            SceneNodes=world.Scene.NodeCount,AmbientActors=world.AmbientActorCount,TerrainChunks=world.ResidentTerrainChunks,
            BatchedStaticInstances=world.StaticInstances,StaticBatches=world.StaticBatches,
            ShippedAssetBytes=assetFiles.Sum(x=>new FileInfo(x).Length),
            Notes="Single local adapter. Frame intervals include renderer readback and UI scheduling. Capture timings include bitmap allocation. First five successful frames are warmup. No cross-hardware claim."
        };
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);
        File.WriteAllText(output,JsonSerializer.Serialize(report,new JsonSerializerOptions { WriteIndented=true }));
        return $"{report.ActualFramesPerSecond:N1} FPS, p95 {report.FrameIntervalP95Ms:N1} ms, {report.MeanDrawCalls:N0} draws, {report.Adapter}";
    }
}
