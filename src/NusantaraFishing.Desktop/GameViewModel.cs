using CommunityToolkit.Mvvm.Input;
using NusantaraFishing.Core;
using NusantaraFishing.Core.Networking;
using ReactiveUI;

namespace NusantaraFishing.Desktop;

public sealed class GameViewModel : ReactiveObject
{
    public GameSession Game { get; }
    public IRelayCommand CastCommand { get; }
    public IRelayCommand SellCommand { get; }
    public IRelayCommand UpgradeCommand { get; }
    public IRelayCommand BoatCommand { get; }
    public IRelayCommand BossCommand { get; }
    public string Summary => Localization.T($"LV {Game.Player.Level:00}   •   {Game.Player.Coins:N0} koin   •   {Game.Player.Discovered.Count} penemuan");
    public string Status => Localization.T(Notice??(Game.Busy && Game.BossCue.Length > 0 ? Game.BossCue : Game.Message));
    public string? Notice { get; set; }
    public Action<ActionRequest>? NetworkDispatch { get; set; }
    public event Action? StateChanged;
    public GameViewModel(GameSession game)
    {
        Game = game;
        CastCommand = new RelayCommand(() => Execute("cast"));
        SellCommand = new RelayCommand(() => Execute("sell"));
        UpgradeCommand = new RelayCommand(() => Execute("rod"));
        BoatCommand = new RelayCommand(() => Execute("boat"));
        BossCommand = new RelayCommand(() => Execute("boss"));
    }
    public bool Execute(string kind,string? target=null,int slot=0)
    {
        Notice=null;
        if(NetworkDispatch is not null) { NetworkDispatch(new(kind,target,slot)); return false; }
        var result=kind switch
        {
            "cast"=>Game.Cast(),"boss"=>Game.Cast(true),"rod"=>Game.UpgradeRod(),"boat"=>Game.BuyNextBoat(),
            "travel"=>Game.Travel(target??""),"character"=>Game.SelectCharacter(target??""),"bait"=>Game.SelectBait(target??""),
            "ability"=>Game.ActivateAbility(),"board"=>Game.ToggleBoarding(),"jump"=>Game.Jump(),
            "method"=>Game.SelectCatchingMethod(target??""),"barter"=>Game.Barter(target??""),
            "climb"=>Game.ClimbTree(),
            "donate"=>Game.Donate(target??""),"place"=>Game.PlaceExhibit(target??"",slot),
            "aquarium"=>Game.UpgradeExhibition(false),"museum"=>Game.UpgradeExhibition(true),
            "quest"=>Game.ClaimQuest(target??""),"sell"=>Sell(),_=>false
        };
        Refresh(); return result;
    }
    private bool Sell() { if(Game.Busy) return false; Game.SellAll(); return true; }
    public void Refresh()
    {
        this.RaisePropertyChanged(nameof(Summary));
        this.RaisePropertyChanged(nameof(Status));
        StateChanged?.Invoke();
    }
}
