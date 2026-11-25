namespace CamAda.Core.Assistant;

/// <summary>Bir ayın tahmini harcaması ve sayıları; <see cref="Month"/> "yyyy-MM".</summary>
public sealed record UsageMonth(string Month, decimal EstimatedUsd, long Sessions, long Searches);

public static class UsageLedger
{
    /// <summary>Aynı aya ekler; ay değişmişse yeni ay yalnız yeni değerlerle başlar.</summary>
    public static UsageMonth Add(UsageMonth? current, DateOnly today, decimal usd, int sessions, int searches)
    {
        var month = today.ToString("yyyy-MM", System.Globalization.CultureInfo.InvariantCulture);
        return current is null || current.Month != month
            ? new UsageMonth(month, usd, sessions, searches)
            : current with
            {
                EstimatedUsd = current.EstimatedUsd + usd,
                Sessions = current.Sessions + sessions,
                Searches = current.Searches + searches,
            };
    }
}
