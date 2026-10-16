using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace NusantaraFishing.Core.Networking;

/// <summary>Ordered HTTP transport. Retries/reconnects reuse identity rather than importing local progress.</summary>
public sealed class CoopClient : IDisposable
{
    private readonly HttpClient http;
    private readonly SemaphoreSlim ordered=new(1,1);
    private readonly JsonSerializerOptions json=new(JsonSerializerDefaults.Web);
    private string token="";
    private long sequence;
    private JoinRequest? identity;
    public RoomView? LastView { get; private set; }
    public Uri Address=>http.BaseAddress!;
    public CoopClient(string address)
    {
        if(!Uri.TryCreate(address.EndsWith('/')?address:address+"/",UriKind.Absolute,out var uri) ||
            uri.Scheme is not ("http" or "https") || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0)
            throw new ArgumentException("Enter an HTTP or HTTPS server address.",nameof(address));
        http=new() { BaseAddress=uri,Timeout=TimeSpan.FromSeconds(5),MaxResponseContentBufferSize=1048576 };
    }
    public async Task<JoinReply> JoinAsync(JoinRequest request,CancellationToken cancellationToken=default)
    {
        await ordered.WaitAsync(cancellationToken);
        try { return await JoinCore(request,cancellationToken); }
        finally { ordered.Release(); }
    }
    private async Task<JoinReply> JoinCore(JoinRequest request,CancellationToken ct)
    {
        using var response=await http.PostAsJsonAsync("api/join",request,json,ct);
        var reply=await Read<JoinReply>(response,ct);
        token=reply.Token; sequence=Math.Max(sequence,reply.LastSequence);
        identity=request with { ResumeToken=token };
        http.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",token);
        LastView=reply.View;
        return reply;
    }
    public async Task<JoinReply> ReconnectAsync(CancellationToken ct=default)
    {
        await ordered.WaitAsync(ct);
        try { return await JoinCore(identity??throw new InvalidOperationException("Join first."),ct); }
        finally { ordered.Release(); }
    }
    public Task<CommandReply> SendAsync(ActionRequest request,CancellationToken ct=default)=>
        CommandAsync(seq=>new(seq,request.Kind,request.Target,request.Slot),ct);
    public Task<CommandReply> InputAsync(float x,float z,bool reel,float direction,CancellationToken ct=default)=>
        CommandAsync(seq=>new(seq,"input",X:x,Z:z,Reel:reel,Direction:direction),ct);
    private async Task<CommandReply> CommandAsync(Func<long,PlayerCommand> make,CancellationToken ct)
    {
        await ordered.WaitAsync(ct);
        try
        {
            if(token.Length==0) throw new InvalidOperationException("Join first.");
            var command=make(checked(++sequence));
            using var response=await http.PostAsJsonAsync("api/command",command,json,ct);
            var reply=await Read<CommandReply>(response,ct);
            LastView=reply.View; return reply;
        }
        finally { ordered.Release(); }
    }
    public async Task<LeaderboardView> LeaderboardAsync(string cadence,bool previous=false,CancellationToken ct=default)
    {
        using var response=await http.GetAsync("api/leaderboards/"+Uri.EscapeDataString(cadence)+"?previous="+(previous?"true":"false"),ct);
        return await Read<LeaderboardView>(response,ct);
    }
    public async Task LeaveAsync(CancellationToken ct=default)
    {
        using var response=await http.PostAsync("api/leave",null,ct);
        if(!response.IsSuccessStatusCode) await Error(response,ct);
    }
    private async Task<T> Read<T>(HttpResponseMessage response,CancellationToken ct)
    {
        if(!response.IsSuccessStatusCode) await Error(response,ct);
        if(response.Content.Headers.ContentLength is >1048576) throw new InvalidDataException("Server response is too large.");
        return await response.Content.ReadFromJsonAsync<T>(json,ct)??throw new InvalidDataException("Empty server response.");
    }
    private static async Task Error(HttpResponseMessage response,CancellationToken ct)
    {
        var message="Server returned "+(int)response.StatusCode;
        if(response.Content.Headers.ContentLength is <=16384)
        {
            var text=await response.Content.ReadAsStringAsync(ct);
            try { using var document=JsonDocument.Parse(text); message=document.RootElement.GetProperty("error").GetString()??message; }
            catch(Exception ex) when(ex is JsonException or KeyNotFoundException) { }
        }
        throw new NetworkRuleException(message,(int)response.StatusCode);
    }
    // In-flight requests still release the managed gate while cancellation unwinds.
    public void Dispose() { http.Dispose(); }
}
