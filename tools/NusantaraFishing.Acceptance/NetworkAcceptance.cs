using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using NusantaraFishing.Core;
using NusantaraFishing.Core.Networking;

namespace NusantaraFishing.Acceptance;

public static class NetworkAcceptance
{
    public static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding=Encoding.UTF8;
        var delay=75;
        var delayIndex=Array.IndexOf(args,"--delay-ms");
        if(delayIndex>=0 && delayIndex+1<args.Length) delay=Math.Clamp(int.Parse(args[delayIndex+1]),0,250);
        var root=FindRoot();
        var temporaryRoot=Path.GetFullPath(Path.GetTempPath());
        var folder=Path.GetFullPath(Path.Combine(temporaryRoot,"nfm-network-"+Guid.NewGuid().ToString("N")));
        if(!folder.StartsWith(temporaryRoot,StringComparison.OrdinalIgnoreCase) || !Path.GetFileName(folder).StartsWith("nfm-network-",StringComparison.Ordinal))
            throw new InvalidOperationException("Temporary fixture path escaped its target directory.");
        Directory.CreateDirectory(folder);
        var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
        var port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
        var address=$"http://127.0.0.1:{port}/";
        var logs=new ConcurrentQueue<string>();
        var clients=new CoopClient[4];
        Process? server=null;
        using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(3));
        var ct=deadline.Token;
        var timer=Stopwatch.StartNew();
        var checks=new List<string>();
        try
        {
            // Fixture identities are trusted server-side test data, never an import endpoint.
            var tokens=Enumerable.Range(0,4).Select(_=>Convert.ToHexString(RandomNumberGenerator.GetBytes(32))).ToArray();
            var state=new CoopStoredState();
            var yesterday=TournamentPeriods.Get("daily",DateTimeOffset.UtcNow,true);
            for(var i=0;i<4;i++)
            {
                state.Accounts.Add(new() { Id="acceptance-"+i,Name="Client "+i,Room="acceptance",TokenHash=CoopPersistence.HashToken(tokens[i]),
                    Player=new() { Coins=1000,Experience=300,RodLevel=3,TotalCaught=10 } });
                state.Scores.Add(new() { PlayerId="acceptance-"+i,Name="Client "+i,Period=yesterday.Key,StartsAt=yesterday.Start,EndsAt=yesterday.End,Score=100-i,Catches=1,LargestWeight=1 });
            }
            CoopPersistence.Save(Path.Combine(folder,"server-state.json"),state);
            server=Start(root,folder,address,logs);
            await Ready(address,server,ct);
            var joined=new JoinReply[4];
            for(var i=0;i<4;i++)
            {
                clients[i]=new(address);
                joined[i]=await clients[i].JoinAsync(new("Client "+i,"acceptance",ResumeToken:tokens[i]),ct);
            }
            Check(joined[3].View.Players.Length==4,"four independent clients joined");
            using(var extra=new CoopClient(address))
            {
                try { await extra.JoinAsync(new("Fifth","acceptance"),ct); throw new InvalidOperationException("Fifth client was accepted."); }
                catch(NetworkRuleException ex) when(ex.StatusCode==409) { Check(true,"co-op capacity rejects fifth client"); }
                try { await extra.JoinAsync(new("Old","other",Protocol:999),ct); throw new InvalidOperationException("Wrong protocol was accepted."); }
                catch(NetworkRuleException ex) when(ex.StatusCode==426) { Check(true,"protocol mismatch rejected over HTTP"); }
                var fresh=await extra.JoinAsync(new("Fresh","fresh-profile"),ct);
                Check(fresh.View.Self.Player.Coins==300 && fresh.View.Self.Player.TotalCaught==0,"online identity starts with server-owned progress");
                await extra.LeaveAsync(ct);
            }
            var views=joined.Select(x=>x.View).ToArray();
            for(var frame=0;frame<12;frame++)
            {
                var inputs=await Task.WhenAll(clients.Select((client,index)=>client.InputAsync(index<2?1:-1,0,false,0,ct)));
                views=inputs.Select(x=>x.View).ToArray();
                await Task.Delay(100+delay,ct);
            }
            Check(views[0].Players.Any(x=>x.Id==joined[1].PlayerId && x.X>.5f) && views[0].Players.Any(x=>x.Id==joined[2].PlayerId && x.X<-.5f),"movement replicated to another client");
            for(var i=0;i<4;i++) views[i]=(await clients[i].SendAsync(new("boss"),ct)).View;
            var bossTimer=Stopwatch.StartNew();
            var observedFighting=false;
            var reconnected=false;
            while(bossTimer.Elapsed<TimeSpan.FromSeconds(110))
            {
                var replies=await Task.WhenAll(clients.Select((client,index)=>
                {
                    var s=views[index].Self;
                    return client.InputAsync(0,0,s.Phase==FishingPhase.Fighting && s.Tension<.6 && !s.BossCue.Contains("SERANGAN",StringComparison.Ordinal),
                        (float)Math.Sign(s.FishDirection)*.8f,ct);
                }));
                views=replies.Select(x=>x.View).ToArray();
                observedFighting|=views[0].Players.Count(x=>x.Phase==FishingPhase.Fighting)>=2;
                if(views[0].Boss is { Health:<=0 }) break;
                if(!reconnected && views[0].Boss is { Contributors:4,Health:>0 })
                {
                    await clients[3].LeaveAsync(ct); clients[3].Dispose();
                    await Task.Delay(200+delay,ct);
                    clients[3]=new(address);
                    var resumed=await clients[3].JoinAsync(new("Client 3","acceptance",ResumeToken:tokens[3]),ct);
                    views[3]=resumed.View;
                    Check(resumed.PlayerId==joined[3].PlayerId,"disconnect/reconnect resumes identity during encounter");
                    reconnected=true;
                }
                for(var i=0;i<4;i++)
                    if(views[i].Boss is { Health:>0 } && views[i].Self.Phase is FishingPhase.Landed or FishingPhase.Escaped)
                        views[i]=(await clients[i].SendAsync(new("boss"),ct)).View;
                await Task.Delay(100+delay,ct);
            }
            Check(observedFighting,"fishing phases replicated to peers");
            Check(views[0].Boss is { Health:<=0,Contributors:4 },"four clients defeated shared boss with injected delay");
            for(var i=0;i<4;i++)
            {
                views[i]=(await clients[i].InputAsync(0,0,false,0,ct)).View;
                Check(views[i].Self.Player.Inventory.Count==1 && views[i].Self.Player.Inventory[0].SpeciesId=="boss-selatan","exactly one boss reward for client "+i);
            }
            // Replay an old request through a raw HTTP connection; it must be ignored.
            using(var replay=new HttpClient { BaseAddress=new(address) })
            {
                replay.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",tokens[0]);
                using var response=await replay.PostAsJsonAsync("api/command",new PlayerCommand(1,"boss"),ct);
                var body=await response.Content.ReadFromJsonAsync<CommandReply>(cancellationToken:ct);
                Check(body is { Accepted:false } && body.View.Self.Player.Inventory.Count==1,"replayed command cannot duplicate reward");
            }
            var board=await clients[0].LeaderboardAsync("daily",ct:ct);
            Check(board.Entries.Length==4 && board.Entries.All(x=>x.Catches==1),"server-owned tournament scores match catches");
            await Stop(server); server=null;
            server=Start(root,folder,address,logs);
            await Ready(address,server,ct);
            for(var i=0;i<4;i++)
            {
                var resumed=await clients[i].ReconnectAsync(ct);
                Check(resumed.View.Self.Player.Inventory.Count==1,"restart preserves one reward for client "+i);
            }
            var before=(await clients[0].InputAsync(0,0,false,0,ct)).View.Self.Player.Coins;
            var claim=await clients[0].SendAsync(new("tournament","daily"),ct);
            var duplicate=await clients[0].SendAsync(new("tournament","daily"),ct);
            Check(claim.Accepted && !duplicate.Accepted && duplicate.View.Self.Player.Coins==before+1000,"ended tournament prize is paid once");
            var output=Path.Combine(root,"artifacts/acceptance"); Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output,"network.json"),JsonSerializer.Serialize(new
            { Passed=true,Clients=4,InjectedClientDelayMs=delay,Seconds=timer.Elapsed.TotalSeconds,OperatingSystem=Environment.OSVersion.ToString(),Runtime=Environment.Version.ToString(),Checks=checks },new JsonSerializerOptions { WriteIndented=true }));
            Console.WriteLine($"NETWORK_ACCEPTANCE_OK: {checks.Count} checks, 4 HTTP clients, delay {delay} ms, {timer.Elapsed.TotalSeconds:N1} s.");
            return 0;
        }
        catch(Exception ex)
        {
            Console.Error.WriteLine("NETWORK_ACCEPTANCE_FAILED: "+ex);
            foreach(var line in logs.TakeLast(12)) Console.Error.WriteLine(line);
            return 1;
        }
        finally
        {
            foreach(var client in clients) client?.Dispose();
            if(server is not null) await Stop(server);
            // Resolved path was verified beneath the explicit temp root before creation.
            if(Directory.Exists(folder)) Directory.Delete(folder,true);
        }
        void Check(bool condition,string name)
        { if(!condition) throw new InvalidOperationException(name); checks.Add(name); Console.WriteLine("PASS: "+name); }
    }
    private static string FindRoot()
    {
        for(var directory=new DirectoryInfo(Directory.GetCurrentDirectory());directory is not null;directory=directory.Parent)
            if(File.Exists(Path.Combine(directory.FullName,"NusantaraFishingMadness.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Run from the repository root.");
    }
    private static Process Start(string root,string folder,string address,ConcurrentQueue<string> logs)
    {
        var dll=Path.Combine(root,"src/NusantaraFishing.Server/bin/Release/net10.0/NusantaraFishing.Server.dll");
        if(!File.Exists(dll)) throw new FileNotFoundException("Build the solution in Release first.",dll);
        var start=new ProcessStartInfo("dotnet") { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=root };
        foreach(var argument in new[]{dll,"--urls",address,"--DataDirectory",folder}) start.ArgumentList.Add(argument);
        var process=Process.Start(start)??throw new InvalidOperationException("Server did not start.");
        void Log(object sender,DataReceivedEventArgs e)
        {
            if(e.Data is null) return;
            logs.Enqueue(e.Data); while(logs.Count>40) logs.TryDequeue(out _);
        }
        process.OutputDataReceived+=Log; process.ErrorDataReceived+=Log;
        process.BeginOutputReadLine(); process.BeginErrorReadLine(); return process;
    }
    private static async Task Ready(string address,Process process,CancellationToken ct)
    {
        using var client=new HttpClient { Timeout=TimeSpan.FromSeconds(1) };
        for(var i=0;i<50;i++)
        {
            if(process.HasExited) throw new InvalidOperationException("Server exited with "+process.ExitCode);
            try { using var response=await client.GetAsync(address+"health",ct); if(response.IsSuccessStatusCode) return; }
            catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException) { if(ct.IsCancellationRequested) throw; }
            await Task.Delay(100,ct);
        }
        throw new TimeoutException("Server health check timed out.");
    }
    private static async Task Stop(Process process)
    {
        if(!process.HasExited)
        {
            try { await process.StandardInput.WriteLineAsync("stop"); await process.StandardInput.FlushAsync(); } catch(IOException) { }
            using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try { await process.WaitForExitAsync(timeout.Token); }
            catch(OperationCanceledException) { if(!process.HasExited) process.Kill(true); }
        }
        process.Dispose();
    }
}
