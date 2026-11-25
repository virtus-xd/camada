namespace CamAda.Core.Assistant;

/// <summary>
/// Tahmini maliyet (yalnız bilgi amaçlı; asıl tutar AI Studio'dadır). Live fiyatları tarihli satırlarda tutulur; Google
/// fiyat değiştirince yeni bir satır eklenir. Kaynak: AI Studio fiyat sayfası, 25.11.2026.
/// </summary>
public static class CostTable
{
    public const int FreeSearchesPerMonth = 5000;
    public const decimal SearchPrice = 0.014m;
    public const decimal LookupInputPerMillion = 0.30m;
    public const decimal LookupOutputPerMillion = 2.50m;

    private static readonly LiveRates[] Rates =
    [
        new(new DateOnly(2026, 1, 1), TextIn: 0.75m, AudioIn: 3.00m, OtherIn: 1.00m, TextOut: 4.50m, AudioOut: 12.00m),
    ];

    /// <summary>Bir Live yanıtının maliyeti; düşünme tokenları metin çıkışı, araç tokenları metin girişi sayılır.</summary>
    public static decimal Live(UsageReport u, DateOnly date)
    {
        var r = Rates.LastOrDefault(rate => rate.From <= date) ?? Rates[0];
        decimal total = 0;
        foreach (var (modality, count) in u.Prompt)
        {
            total += count * modality switch { "TEXT" => r.TextIn, "AUDIO" => r.AudioIn, _ => r.OtherIn };
        }

        foreach (var (modality, count) in u.Response)
        {
            total += count * (modality == "AUDIO" ? r.AudioOut : r.TextOut);
        }

        total += u.Thoughts * r.TextOut + u.ToolUsePrompt * r.TextIn;
        return total / 1_000_000m;
    }

    /// <summary>Güncel bilgi araması: tokenlar ve ayın ilk 5.000'ini aşan aramalar.</summary>
    public static decimal Lookup(long promptTokens, long outputTokens, long searchesBeforeThisMonth, int searches)
    {
        var paid = Math.Max(0, searchesBeforeThisMonth + searches - Math.Max(searchesBeforeThisMonth, FreeSearchesPerMonth));
        return (promptTokens * LookupInputPerMillion + outputTokens * LookupOutputPerMillion) / 1_000_000m + paid * SearchPrice;
    }

    private sealed record LiveRates(DateOnly From, decimal TextIn, decimal AudioIn, decimal OtherIn, decimal TextOut, decimal AudioOut);
}
