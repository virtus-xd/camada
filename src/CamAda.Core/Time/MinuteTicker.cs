namespace CamAda.Core.Time;

/// <summary>Saati dakika sınırında güncellemek için bir sonraki tetiklenme süresini hesaplar.</summary>
public static class MinuteTicker
{
    public static TimeSpan DelayToNextMinute(DateTime local)
    {
        var intoMinute = TimeSpan.FromTicks(local.Ticks % TimeSpan.TicksPerMinute);
        return TimeSpan.FromMinutes(1) - intoMinute;
    }
}
