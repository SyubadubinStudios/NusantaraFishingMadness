using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using NusantaraFishing.Core;
using NusantaraFishing.Core.Networking;

namespace NusantaraFishing.Desktop;

public sealed partial class MainWindow
{
    private CoopClient? network;
    private RoomView? roomView;
    private SimulationSnapshot? localProgress;
    private CancellationTokenSource? networkLifetime;
    private bool networkPolling;
    private bool joining;
    private bool isClosing;
    private readonly CancellationTokenSource windowLifetime=new();
    private double lastNetworkPoll;
    private double retryAfter;
    private Process? hostedServer;
    private readonly TextBlock coopStatus=Text("",12,Muted);
    private readonly ProgressBar sharedBossHealth=new() { Minimum=0,Maximum=1,Height=9 };
    private string lastServer="http://127.0.0.1:5077";
    private string lastRoom="harbor";
    private string lastName="Pemancing";
    private RoomMode lastMode;

    private void BuildNetworkPage()
    {
        Heading("Bersama di perairan","Co-op & festival","Server menyimpan progres online terpisah. Co-op hingga 4 pemain; festival hingga 32 pemain.");
        sidebar.Children.Add(coopStatus);
        if(network is null)
        {
            sidebar.Children.Add(Text("Alamat server",13,Foreground!,true));
            var address=new TextBox { Text=lastServer,PlaceholderText="http://127.0.0.1:5077" };
            sidebar.Children.Add(address);
            sidebar.Children.Add(Text("Nama pemain",13,Foreground!,true));
            var name=new TextBox { Text=lastName,MaxLength=24 };
            sidebar.Children.Add(name);
            sidebar.Children.Add(Text("Ruang",13,Foreground!,true));
            var room=new TextBox { Text=lastRoom,MaxLength=24 };
            sidebar.Children.Add(room);
            var mode=new ComboBox { ItemsSource=new[]{"Co-op (4)","Festival (32)"},SelectedIndex=(int)lastMode,HorizontalAlignment=HorizontalAlignment.Stretch };
            sidebar.Children.Add(mode);
            var fresh=new CheckBox { Content=Localization.T("Buat identitas online baru"),IsChecked=false };
            sidebar.Children.Add(fresh);
            var join=Button("Gabung server",async()=>
            { await ConnectAsync(address.Text??"",name.Text??"",room.Text??"",(RoomMode)Math.Max(0,mode.SelectedIndex),fresh.IsChecked==true); },true);
            join.IsEnabled=!joining; sidebar.Children.Add(join);
            var host=Button("Host lokal & gabung",async()=>
            {
                try { await HostLocalAsync(); await ConnectAsync("http://127.0.0.1:5077",name.Text??"",room.Text??"",(RoomMode)Math.Max(0,mode.SelectedIndex),fresh.IsChecked==true); }
                catch(Exception ex) when(ex is IOException or InvalidOperationException or System.ComponentModel.Win32Exception or HttpRequestException)
                { NetworkNotice(ex.Message); }
            });
            host.IsEnabled=!joining; sidebar.Children.Add(host);
            sidebar.Children.Add(Text("Host lokal memakai port 5077 dan berhenti saat aplikasi host ditutup. Untuk perangkat lain, jalankan server dengan alamat LAN sesuai panduan.",12,Muted));
            return;
        }
        sidebar.Children.Add(Button("Keluar ke permainan lokal",async()=>await DisconnectAsync(),true));
        if(roomView is { } current)
        {
            sidebar.Children.Add(Text(current.Room+" • "+current.Mode,18,Foreground!,true));
            foreach(var peer in current.Players)
                sidebar.Children.Add(Text($"{peer.Name} • Lv. {peer.Level} • {Catalog.Regions.First(x=>x.Id==peer.RegionId).Name}",12,Muted));
            sidebar.Children.Add(Text("HP boss bersama",15,Foreground!,true));
            sidebar.Children.Add(sharedBossHealth);
        }
        sidebar.Children.Add(Text("Turnamen",19,Foreground!,true));
        var cadence=new ComboBox { ItemsSource=new[]{"daily","weekly","seasonal"},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch };
        var previous=new CheckBox { Content=Localization.T("Periode sebelumnya"),IsChecked=false };
        sidebar.Children.Add(cadence); sidebar.Children.Add(previous);
        var scores=new StackPanel { Spacing=8 };
        sidebar.Children.Add(Button("Muat papan peringkat",async()=>
        {
            var client=network;
            if(client is null) return;
            try
            {
                var board=await client.LeaderboardAsync((string?)cadence.SelectedItem??"daily",previous.IsChecked==true,networkLifetime!.Token);
                if(!ReferenceEquals(network,client)) return;
                scores.Children.Clear();
                scores.Children.Add(Text(board.Period,13,Foreground!,true));
                scores.Children.Add(Text($"UTC: {board.StartsAt:dd MMM} → {board.EndsAt:dd MMM}",11,Muted));
                foreach(var row in board.Entries) scores.Children.Add(Text($"{row.Rank}. {row.Name} • {row.Score:N0} • {row.Catches} tangkapan",12,Muted));
                if(board.Entries.Length==0) scores.Children.Add(Text("Belum ada hasil pada periode ini.",12,Muted));
            }
            catch(Exception ex) when(ex is HttpRequestException or NetworkRuleException or OperationCanceledException or InvalidDataException)
            { if(ReferenceEquals(network,client)) NetworkNotice(ex.Message); }
        }));
        sidebar.Children.Add(scores);
        foreach(var period in new[]{"daily","weekly","seasonal"})
            sidebar.Children.Add(Button("Ambil hadiah "+period,()=>vm.Execute("tournament",period)));
        sidebar.Children.Add(Text("Hadiah hanya untuk periode yang sudah berakhir dan hanya bisa diambil sekali. Skor dihitung server dari tangkapan, bukan dari data lokal.",11,Muted));
    }

    private void NetworkNotice(string text)
    { coopStatus.Text=Localization.T(text); vm.Notice=text; vm.Refresh(); }

    private async Task ConnectAsync(string address,string name,string room,RoomMode mode,bool fresh)
    {
        if(joining || network is not null || vm.Game.Busy) { NetworkNotice("Selesaikan pertarungan sebelum bergabung."); return; }
        joining=true;
        CoopClient? client=null;
        using var timeout=CancellationTokenSource.CreateLinkedTokenSource(windowLifetime.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        try
        {
            client=new CoopClient(address);
            var token=fresh?null:ReadIdentity(client.Address.AbsoluteUri,room,mode);
            var joined=await client.JoinAsync(new(name,room,mode,ResumeToken:token),timeout.Token);
            if(vm.Game.Busy) { await client.LeaveAsync(timeout.Token); throw new InvalidOperationException("Selesaikan pertarungan sebelum bergabung."); }
            Save();
            localProgress=vm.Game.Snapshot();
            network=client; networkLifetime=CancellationTokenSource.CreateLinkedTokenSource(windowLifetime.Token);
            lastServer=client.Address.AbsoluteUri; lastName=name; lastRoom=room; lastMode=mode;
            WriteIdentity(lastServer,room,mode,joined.Token);
            vm.NetworkDispatch=SendNetworkAction;
            vm.Notice=null;
            ApplyRoom(joined.View,true);
            NetworkNotice("Terhubung. Progres online disimpan di server.");
        }
        catch(Exception ex) when(ex is HttpRequestException or NetworkRuleException or OperationCanceledException or ArgumentException or IOException or JsonException or InvalidOperationException)
        {
            if(client is not null && ReferenceEquals(network,client)) await DisconnectAsync(); else client?.Dispose();
            if(!isClosing) NetworkNotice(ex.Message);
        }
        finally { joining=false; if(!isClosing && page=="Multiplayer") ShowPage(page); }
    }

    private async void SendNetworkAction(ActionRequest action)
    {
        var client=network; var lifetime=networkLifetime;
        if(client is null || lifetime is null) return;
        try
        {
            var reply=await client.SendAsync(action,lifetime.Token);
            if(!ReferenceEquals(network,client) || lifetime.IsCancellationRequested) return;
            vm.Notice=reply.Accepted?null:reply.Error;
            ApplyRoom(reply.View,true);
        }
        catch(Exception ex) when(ex is HttpRequestException or NetworkRuleException or OperationCanceledException or InvalidDataException or JsonException)
        { if(ReferenceEquals(network,client) && !lifetime.IsCancellationRequested) NetworkNotice("Aksi belum terkonfirmasi. "+ex.Message); }
    }

    private async void PollNetwork(Vector2Input input,double now)
    {
        var client=network; var lifetime=networkLifetime;
        if(client is null || lifetime is null || networkPolling || now-lastNetworkPoll<.1 || now<retryAfter) return;
        networkPolling=true; lastNetworkPoll=now;
        try
        {
            var reply=await client.InputAsync(input.X,input.Z,input.Reel,input.Direction,lifetime.Token);
            if(ReferenceEquals(network,client) && !lifetime.IsCancellationRequested) ApplyRoom(reply.View,false);
        }
        catch(Exception ex) when(ex is HttpRequestException or NetworkRuleException or OperationCanceledException or InvalidDataException or JsonException)
        {
            if(!ReferenceEquals(network,client) || lifetime.IsCancellationRequested) return;
            NetworkNotice("Koneksi terputus, mencoba identitas yang sama… "+ex.Message);
            retryAfter=now+2;
            try
            {
                var joined=await client.ReconnectAsync(lifetime.Token);
                if(ReferenceEquals(network,client) && !lifetime.IsCancellationRequested) { vm.Notice=null; ApplyRoom(joined.View,true); }
            }
            catch(Exception retry) when(retry is HttpRequestException or NetworkRuleException or OperationCanceledException or InvalidDataException or JsonException)
            { if(ReferenceEquals(network,client) && !lifetime.IsCancellationRequested) coopStatus.Text=Localization.T(retry.Message); }
        }
        finally { networkPolling=false; }
    }
    private readonly record struct Vector2Input(float X,float Z,bool Reel,float Direction);

    private void ApplyRoom(RoomView view,bool refreshPage)
    {
        var region=vm.Game.Player.RegionId;
        var previousPeers=roomView is null?"":string.Join(',',roomView.Players.Select(x=>x.Id));
        vm.Game.ApplySnapshot(view.Self);
        roomView=view;
        if(region!=vm.Game.Player.RegionId) _ = PresentWorldAsync();
        world?.SetPeers(view.Players,view.PlayerId);
        world?.SetSharedBoss(view.Boss);
        coopStatus.Text=$"{view.Room} • {view.Players.Length}/{(view.Mode==RoomMode.Coop?4:32)} • {view.ServerTime:HH:mm:ss} UTC";
        sharedBossHealth.Value=view.Boss is { Maximum:>0 } boss?boss.Health/boss.Maximum:0;
        vm.Refresh();
        var peersChanged=previousPeers!=string.Join(',',view.Players.Select(x=>x.Id));
        if((refreshPage && page is not ("Pengaturan" or "Perairan")) || (peersChanged && page=="Multiplayer")) ShowPage(page);
    }
    private async Task DisconnectAsync()
    {
        var client=network;
        network=null; vm.NetworkDispatch=null; roomView=null;
        networkLifetime?.Cancel(); networkLifetime?.Dispose(); networkLifetime=null;
        if(client is not null)
        {
            try { using var timeout=new CancellationTokenSource(TimeSpan.FromSeconds(2)); await client.LeaveAsync(timeout.Token); }
            catch(Exception ex) when(ex is HttpRequestException or NetworkRuleException or OperationCanceledException) { }
            client.Dispose();
        }
        if(localProgress is not null) { vm.Game.ApplySnapshot(localProgress); localProgress=null; if(!isClosing) await PresentWorldAsync(); }
        vm.Notice=null; coopStatus.Text=Localization.T("Kembali ke progres lokal."); vm.Refresh();
        if(!isClosing && page=="Multiplayer") ShowPage(page);
    }

    private string IdentityPath=>Path.Combine(Path.GetDirectoryName(settingsPath)!,"server-identities.json");
    private static string IdentityKey(string address,string room,RoomMode mode)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(address+"|"+room+"|"+mode)));
    private string? ReadIdentity(string address,string room,RoomMode mode)
    {
        if(snapshotMode || !File.Exists(IdentityPath)) return null;
        return JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(IdentityPath))?.GetValueOrDefault(IdentityKey(address,room,mode));
    }
    private void WriteIdentity(string address,string room,RoomMode mode,string token)
    {
        if(snapshotMode) return;
        var identities=File.Exists(IdentityPath)?JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(IdentityPath))??[]:[];
        identities[IdentityKey(address,room,mode)]=token;
        Directory.CreateDirectory(Path.GetDirectoryName(IdentityPath)!);
        File.WriteAllText(IdentityPath+".tmp",JsonSerializer.Serialize(identities));
        File.Move(IdentityPath+".tmp",IdentityPath,true);
    }
    private async Task HostLocalAsync()
    {
        using var health=new HttpClient { Timeout=TimeSpan.FromSeconds(1) };
        try
        {
            using var existing=await health.GetAsync("http://127.0.0.1:5077/health");
            if(existing.IsSuccessStatusCode)
            {
                var payload=await existing.Content.ReadAsStringAsync();
                try
                {
                    using var json=JsonDocument.Parse(payload);
                    if(json.RootElement.TryGetProperty("protocol",out var protocol) && protocol.TryGetInt32(out var version) && version==CoopProtocol.Version) return;
                }
                catch(JsonException) { }
                throw new InvalidOperationException("Port 5077 is occupied by an incompatible service.");
            }
        }
        catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException) { }
        if(hostedServer is null || hostedServer.HasExited)
        {
            var bundled=Path.Combine(AppContext.BaseDirectory,"server",OperatingSystem.IsWindows()?"NusantaraFishing.Server.exe":"NusantaraFishing.Server");
            var source=Path.GetFullPath("src/NusantaraFishing.Server/NusantaraFishing.Server.csproj");
            var start=new ProcessStartInfo { FileName=File.Exists(bundled)?bundled:"dotnet",UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true };
            if(!File.Exists(bundled))
            {
                if(!File.Exists(source)) throw new FileNotFoundException("Server bundle missing. Run from the repository or publish the server beside the game.");
                foreach(var arg in new[]{"run","--project",source,"--no-launch-profile","--"}) start.ArgumentList.Add(arg);
            }
            start.ArgumentList.Add("--urls"); start.ArgumentList.Add("http://127.0.0.1:5077");
            start.Environment["NFM_DATA_DIRECTORY"]=Path.Combine(Path.GetDirectoryName(settingsPath)!,"server");
            hostedServer=Process.Start(start)??throw new InvalidOperationException("Could not start the local server.");
            hostedServer.OutputDataReceived+=(_,_)=>{}; hostedServer.ErrorDataReceived+=(_,_)=>{};
            hostedServer.BeginOutputReadLine(); hostedServer.BeginErrorReadLine();
        }
        for(var i=0;i<40;i++)
        {
            if(hostedServer.HasExited) throw new InvalidOperationException("Local server exited. Check whether port 5077 or its data directory is already in use.");
            try { using var ready=await health.GetAsync("http://127.0.0.1:5077/health"); if(ready.IsSuccessStatusCode) return; }
            catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException) { }
            await Task.Delay(250);
        }
        throw new InvalidOperationException("Local server startup timed out.");
    }
    private void CloseNetwork()
    {
        isClosing=true;
        windowLifetime.Cancel();
        networkLifetime?.Cancel();
        network?.Dispose();
        if(hostedServer is { HasExited:false })
        {
            try { hostedServer.StandardInput.WriteLine("stop"); hostedServer.StandardInput.Flush(); }
            catch(Exception ex) when(ex is IOException or InvalidOperationException) { }
        }
    }
}
