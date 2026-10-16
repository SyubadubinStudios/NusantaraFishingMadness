using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using NusantaraFishing.Core.Networking;
using NusantaraFishing.Server;

var builder=WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options=>
{
    options.Limits.MaxRequestBodySize=16384;
    options.Limits.MaxConcurrentConnections=128;
    options.Limits.RequestHeadersTimeout=TimeSpan.FromSeconds(10);
});
if(string.IsNullOrEmpty(builder.Configuration["urls"])) builder.WebHost.UseUrls("http://127.0.0.1:5077");
var folder=builder.Configuration["DataDirectory"]??Environment.GetEnvironmentVariable("NFM_DATA_DIRECTORY")??
    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"SyubadubinStudios","NusantaraFishingMadness","server");
Directory.CreateDirectory(Path.GetFullPath(folder));
using var storageLock=new FileStream(Path.Combine(Path.GetFullPath(folder),"server.lock"),FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);
builder.Services.AddSingleton(new CoopService(Path.Combine(Path.GetFullPath(folder),"server-state.json")));
builder.Services.AddHostedService<SimulationWorker>();
builder.Services.AddRateLimiter(options=>
{
    options.RejectionStatusCode=429;
    options.GlobalLimiter=PartitionedRateLimiter.Create<HttpContext,string>(context=>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>
            new FixedWindowRateLimiterOptions { PermitLimit=600,Window=TimeSpan.FromSeconds(1),QueueLimit=0,AutoReplenishment=true }));
    options.AddPolicy("join",context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>
        new FixedWindowRateLimiterOptions { PermitLimit=60,Window=TimeSpan.FromMinutes(1),QueueLimit=0,AutoReplenishment=true }));
});
var app=builder.Build();
app.Use(async(context,next)=>
{
    try { await next(context); }
    catch(NetworkRuleException ex)
    { context.Response.StatusCode=ex.StatusCode; await context.Response.WriteAsJsonAsync(new { error=ex.Message }); }
});
app.UseRateLimiter();
static string Token(HttpContext context)
{
    var header=context.Request.Headers.Authorization.ToString();
    if(!header.StartsWith("Bearer ",StringComparison.OrdinalIgnoreCase)) throw new NetworkRuleException("Session authentication required.",401);
    return header[7..].Trim();
}
app.MapGet("/health",()=>new { status="ready",protocol=CoopProtocol.Version,coopCapacity=CoopProtocol.CoopCapacity,festivalCapacity=CoopProtocol.FestivalCapacity });
app.MapPost("/api/join",(JoinRequest request,CoopService service)=>service.Join(request)).RequireRateLimiting("join");
app.MapGet("/api/state",(HttpContext context,CoopService service)=>service.GetView(Token(context)));
app.MapPost("/api/command",(HttpContext context,PlayerCommand command,CoopService service)=>service.Command(Token(context),command));
app.MapPost("/api/leave",(HttpContext context,CoopService service)=> { service.Leave(Token(context)); return Results.NoContent(); });
app.MapGet("/api/leaderboards/{cadence}",(string cadence,bool? previous,HttpContext context,CoopService service)=>
{ service.GetView(Token(context)); return service.Leaderboard(cadence,previous??false); });
app.Lifetime.ApplicationStopping.Register(()=>app.Services.GetRequiredService<CoopService>().Flush());
// The desktop host can request graceful shutdown through its private stdin pipe.
_ = Task.Run(()=>
{
    while(Console.ReadLine() is { } line)
        if(line=="stop") { app.Lifetime.StopApplication(); break; }
});
app.Run();
