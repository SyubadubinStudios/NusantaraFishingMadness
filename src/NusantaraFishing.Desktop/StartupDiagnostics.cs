using System.Diagnostics;
using System.Text.Json;

namespace NusantaraFishing.Desktop;

/// <summary>Opt-in wall-clock milestones, independent of native frame timestamps.</summary>
internal static class StartupDiagnostics
{
    private static readonly Stopwatch clock=Stopwatch.StartNew();
    private static readonly List<object> stages=[];
    private static bool enabled;
    public static void Start(bool requested)
    { enabled=requested; if(enabled) { clock.Restart(); stages.Clear(); Mark("process-main"); } }
    public static void Mark(string name)
    {
        if(!enabled) return;
        var elapsed=clock.Elapsed.TotalMilliseconds;
        stages.Add(new { Stage=name,ElapsedMs=elapsed });
        Console.WriteLine($"STARTUP_STAGE: {name}, {elapsed:F1} ms");
        if(name!="first-frame-readback") return;
        var path=Path.GetFullPath("artifacts/benchmarks/startup.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path,JsonSerializer.Serialize(new { RecordedAt=DateTimeOffset.UtcNow,Stages=stages },new JsonSerializerOptions { WriteIndented=true }));
    }
}
