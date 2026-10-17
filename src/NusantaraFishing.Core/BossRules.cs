namespace NusantaraFishing.Core;

public static class BossRules
{
    public const string PartyRequired = "Kraken membutuhkan minimal 2 pemain siap di Raja Ampat dalam ruang online yang sama. Setiap pemain perlu joran level 3, 10 tangkapan, dan berada di perahu.";
    public const string WaitingForParty = "Kraken menunggu minimal 2 peserta terhubung. Minta partner menekan Tantang boss regional; batas waktu tetap berjalan.";
    public const string PartyReady = "Tim Kraken siap. Tarik bersama dan lindungi tali saat serangan datang.";

    public static int MinimumPlayers(string regionId) => regionId == "ampat" ? 2 : 1;
}
