using System.Globalization;

namespace NusantaraFishing.Core.Networking;

public static class TournamentPeriods
{
    public static (string Key,DateTimeOffset Start,DateTimeOffset End) Get(string cadence,DateTimeOffset now,bool previous=false)
    {
        now=now.ToUniversalTime();
        if(previous) now=cadence switch
        { "daily"=>now.AddDays(-1),"weekly"=>now.AddDays(-7),"seasonal"=>now.AddMonths(-3),_=>throw new NetworkRuleException("Unknown tournament cadence.") };
        var date=new DateTimeOffset(now.Year,now.Month,now.Day,0,0,0,TimeSpan.Zero);
        return cadence switch
        {
            "daily"=>($"daily:{date:yyyy-MM-dd}",date,date.AddDays(1)),
            "weekly"=>Week(date),
            "seasonal"=>Season(date),
            _=>throw new NetworkRuleException("Choose daily, weekly, or seasonal.")
        };
    }
    private static (string,DateTimeOffset,DateTimeOffset) Week(DateTimeOffset date)
    {
        var start=date.AddDays(-((int)date.DayOfWeek+6)%7);
        return ($"weekly:{ISOWeek.GetYear(start.UtcDateTime)}-W{ISOWeek.GetWeekOfYear(start.UtcDateTime):00}",start,start.AddDays(7));
    }
    private static (string,DateTimeOffset,DateTimeOffset) Season(DateTimeOffset date)
    {
        var quarter=(date.Month-1)/3+1;
        var start=new DateTimeOffset(date.Year,(quarter-1)*3+1,1,0,0,0,TimeSpan.Zero);
        return ($"seasonal:{date.Year}-Q{quarter}",start,start.AddMonths(3));
    }
}
