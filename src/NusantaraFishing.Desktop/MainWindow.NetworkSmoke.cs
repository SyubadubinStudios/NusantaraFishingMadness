using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;
using NusantaraFishing.Core.Networking;
using ThreeNet;
using Key=Avalonia.Input.Key;
using Vector=Avalonia.Vector;

namespace NusantaraFishing.Desktop;

public sealed partial class MainWindow
{
    private bool nativeNetworkMode=>Program.Arguments.Contains("--native-network-test");
    private async Task RunNativeNetworkSmoke()
    {
        var bots=new List<CoopClient>();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(windowLifetime.Token);
        deadline.CancelAfter(TimeSpan.FromSeconds(90));
        var ct=deadline.Token;
        try
        {
            var listener=new TcpListener(IPAddress.Loopback,0); listener.Start();
            var port=((IPEndPoint)listener.LocalEndpoint).Port; listener.Stop();
            var address=$"http://127.0.0.1:{port}/";
            var directory=Path.GetFullPath("artifacts/native-network-"+Guid.NewGuid().ToString("N"));
            var bundled=Path.Combine(AppContext.BaseDirectory,"server",OperatingSystem.IsWindows()?"NusantaraFishing.Server.exe":"NusantaraFishing.Server");
            var source=Path.GetFullPath("src/NusantaraFishing.Server/bin/Release/net10.0/NusantaraFishing.Server.dll");
            var start=new ProcessStartInfo(File.Exists(bundled)?bundled:"dotnet")
            { UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true };
            if(!File.Exists(bundled)) start.ArgumentList.Add(source);
            foreach(var argument in new[]{"--urls",address,"--DataDirectory",directory}) start.ArgumentList.Add(argument);
            hostedServer=Process.Start(start)??throw new InvalidOperationException("Fixture server did not start.");
            hostedServer.OutputDataReceived+=(_,_)=>{}; hostedServer.ErrorDataReceived+=(_,_)=>{};
            hostedServer.BeginOutputReadLine(); hostedServer.BeginErrorReadLine();
            using var health=new HttpClient { Timeout=TimeSpan.FromSeconds(1) };
            var ready=false;
            for(var i=0;i<40;i++)
            {
                if(hostedServer.HasExited) throw new InvalidOperationException("Fixture server exited.");
                try { using var response=await health.GetAsync(address+"health",ct); if(response.IsSuccessStatusCode) { ready=true; break; } }
                catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException) { if(ct.IsCancellationRequested) throw; }
                await Task.Delay(100,ct);
            }
            if(!ready) throw new TimeoutException("Fixture server startup timed out.");
            ShowPage("Multiplayer");
            var nameInput=this.GetVisualDescendants().OfType<TextBox>().First();
            nameInput.Focus();
            nameInput.RaiseEvent(new KeyEventArgs { RoutedEvent=KeyDownEvent,Key=settings.KeyFor("forward") });
            if(keys.Count!=0) throw new InvalidOperationException("Typing in a server field activated movement.");
            FocusManager?.Focus(null);
            Console.WriteLine("TEXT_INPUT_OK: multiplayer fields do not activate game movement.");
            var localCoins=vm.Game.Player.Coins;
            await ConnectAsync(address,"Desktop","native-test",RoomMode.Coop,true);
            if(network is null) throw new InvalidOperationException("Native UI could not join the server.");
            for(var i=0;i<3;i++)
            {
                var bot=new CoopClient(address);
                await bot.JoinAsync(new("Companion "+(i+1),"native-test"),ct);
                bots.Add(bot);
            }
            for(var i=0;i<8;i++)
            {
                await Task.WhenAll(bots.Select((bot,index)=>bot.InputAsync(index==0?1:index==1?-1:0,index==2?-1:0,false,0,ct)));
                await Task.Delay(150,ct);
            }
            var joined=await network.InputAsync(0,0,false,0,ct);
            ApplyRoom(joined.View,true);
            if(joined.View.Players.Length!=4 || world?.RemotePeerCount!=3) throw new InvalidOperationException("Peer avatars were not replicated into the native scene.");
            // Exercise the routed keyboard handler through the online input path.
            var before=vm.Game.Player.PositionZ;
            RaiseEvent(new KeyEventArgs { RoutedEvent=KeyDownEvent,Key=settings.KeyFor("forward") });
            await Task.Delay(500,ct);
            RaiseEvent(new KeyEventArgs { RoutedEvent=KeyUpEvent,Key=settings.KeyFor("forward") });
            await Task.Delay(250,ct);
            if(vm.Game.Player.PositionZ>=before-.1f) throw new InvalidOperationException("Routed movement input did not reach the server.");
            if(gamepads is not null)
            {
                var slot=gamepads.SlotCount;
                gamepads.SetVirtual(slot,GamepadButton.None,new(.6f,0));
                var x=vm.Game.Player.PositionX;
                await Task.Delay(500,ct);
                gamepads.SetVirtual(slot,GamepadButton.None,connected:false);
                await Task.Delay(200,ct);
                if(vm.Game.Player.PositionX<x+.1f) throw new InvalidOperationException("Virtual gamepad input did not reach the server.");
                Console.WriteLine("GAMEPAD_OK: native virtual controller movement replicated.");
            }
            else throw new InvalidOperationException("Native gamepad context unavailable.");
            ShowPage("Multiplayer");
            await Task.Delay(300,ct);
            if(rendererFailed) throw new InvalidOperationException("Peer scene renderer failed.");
            var output=Path.GetFullPath("docs/images/multiplayer.png"); Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            using(var bitmap=new RenderTargetBitmap(new PixelSize((int)Bounds.Width,(int)Bounds.Height),new Vector(96,96)))
            { bitmap.Render(this); bitmap.Save(output,PngBitmapEncoderOptions.Default); }
            await DisconnectAsync();
            if(vm.Game.Player.Coins!=localCoins || vm.Game.Player.Inventory.Count!=0) throw new InvalidOperationException("Online session overwrote local progress.");
            Console.WriteLine("NATIVE_NETWORK_OK: host/join, 4 peers, 3 rendered avatars, routed keys, gamepad, local/online separation.");
        }
        catch(Exception ex)
        { Console.Error.WriteLine("NATIVE_NETWORK_FAILED: "+ex); Environment.ExitCode=1; }
        finally
        {
            foreach(var bot in bots)
            {
                try { await bot.LeaveAsync(ct); } catch(Exception ex) when(ex is HttpRequestException or OperationCanceledException or NetworkRuleException) { }
                bot.Dispose();
            }
            if(!isClosing) Close();
        }
    }
}
