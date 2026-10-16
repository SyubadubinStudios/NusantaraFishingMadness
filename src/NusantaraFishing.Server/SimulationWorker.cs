using NusantaraFishing.Core.Networking;

namespace NusantaraFishing.Server;

public sealed class SimulationWorker(CoopService service) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer=new PeriodicTimer(TimeSpan.FromMilliseconds(50));
        try { while(await timer.WaitForNextTickAsync(stoppingToken)) service.Advance(.05); }
        catch(OperationCanceledException) when(stoppingToken.IsCancellationRequested) { }
        finally { service.Flush(); }
    }
}
