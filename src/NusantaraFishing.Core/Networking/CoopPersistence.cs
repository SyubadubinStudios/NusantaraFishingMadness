using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace NusantaraFishing.Core.Networking;

public sealed class CoopAccount
{
    public string Id { get; set; }=Guid.NewGuid().ToString("N");
    public string Name { get; set; }="Pemancing";
    public string Room { get; set; }="harbor";
    public RoomMode Mode { get; set; }
    public string TokenHash { get; set; }="";
    public PlayerState Player { get; set; }=new();
    public long LastSequence { get; set; }
    public HashSet<string> GrantedEncounters { get; set; }=[];
    public HashSet<string> ClaimedPeriods { get; set; }=[];
}
public sealed class CoopBossState
{
    public string Id { get; set; }=Guid.NewGuid().ToString("N");
    public string Room { get; set; }="harbor";
    public string Region { get; set; }="selatan";
    public double Health { get; set; }
    public double Maximum { get; set; }
    public double CompletedAt { get; set; }
    public double StartedAt { get; set; }
    public bool Expired { get; set; }
    public Dictionary<string,int> Contributions { get; set; }=[];
    public bool Rewarded { get; set; }
}
public sealed class TournamentScore
{
    public string Period { get; set; }="";
    public string PlayerId { get; set; }="";
    public string Name { get; set; }="";
    public long Score { get; set; }
    public int Catches { get; set; }
    public double LargestWeight { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset EndsAt { get; set; }
}
public sealed class CoopStoredState
{
    public int Version { get; set; }=1;
    public double ElapsedSeconds { get; set; }
    public double WorldHours { get; set; }=6.5;
    public List<CoopAccount> Accounts { get; set; }=[];
    public List<CoopBossState> Bosses { get; set; }=[];
    public List<TournamentScore> Scores { get; set; }=[];
}
public static class CoopPersistence
{
    public static string HashToken(string token)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static CoopStoredState Load(string path)
    {
        if(!File.Exists(path)) return new();
        var state=JsonSerializer.Deserialize<CoopStoredState>(File.ReadAllText(path))??throw new InvalidDataException("Empty server state.");
        if(state.Version!=1 || !double.IsFinite(state.ElapsedSeconds) || state.ElapsedSeconds<0 ||
            !double.IsFinite(state.WorldHours) || state.WorldHours is <0 or >=24 ||
            state.Accounts is null || state.Bosses is null || state.Scores is null || state.Accounts.Count>1024 ||
            state.Accounts.Select(x=>x.Id).Distinct().Count()!=state.Accounts.Count || state.Accounts.Select(x=>x.TokenHash).Distinct().Count()!=state.Accounts.Count)
            throw new InvalidDataException("Invalid server state.");
        foreach(var account in state.Accounts)
        {
            SaveStore.Validate(account.Player);
            if(account.TokenHash.Length!=64 || account.TokenHash.Any(x=>!Uri.IsHexDigit(x)) || account.LastSequence<0 ||
                account.GrantedEncounters is null || account.ClaimedPeriods is null || !Enum.IsDefined(account.Mode))
                throw new InvalidDataException("Invalid server account.");
            account.Player.Version=2;
        }
        if(state.Bosses.Any(x=>!double.IsFinite(x.Health) || !double.IsFinite(x.Maximum) || x.Health<0 || x.Health>x.Maximum ||
            x.Maximum<=0 || x.Contributions is null || !Catalog.Regions.Any(r=>r.Id==x.Region)))
            throw new InvalidDataException("Invalid persisted boss.");
        return state;
    }
    public static void Save(string path,CoopStoredState state)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        File.WriteAllText(path+".tmp",JsonSerializer.Serialize(state,new JsonSerializerOptions { WriteIndented=true }));
        File.Move(path+".tmp",path,true);
    }
}
