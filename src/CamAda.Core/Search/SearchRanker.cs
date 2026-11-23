using System.Globalization;
using System.Text;

namespace CamAda.Core.Search;

/// <summary>
/// Başlat panelinin araması. Türkçe kurallarla küçük harfe çevirir (I→ı, İ→i), sonra ı/i ve aksanları (ş/s, ç/c, ğ/g,
/// ö/o, ü/u) eşitler; böylece "AYAR", "ıs", "İndir" ve Türkçe karaktersiz yazılan "isik" de bulunur.
/// Puan: ad sorguyla başlıyor 3, bir kelimesi sorguyla başlıyor 2, adın içinde geçiyor 1; eşleşmeyen elenir.
/// </summary>
public static class SearchRanker
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static IReadOnlyList<SearchItem> Rank(string query, IReadOnlyList<SearchItem> items, int limit = 6)
    {
        query = query.Trim();
        if (query.Length == 0)
        {
            return [];
        }

        var folded = Fold(query);
        var results = items
            .Select(item => (Item: item, Score: Score(Fold(item.Name), folded)))
            .Where(r => r.Score > 0)
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Item.Kind)
            .ThenBy(r => r.Item.Name, StringComparer.Create(Turkish, ignoreCase: true))
            .Take(limit)
            .Select(r => r.Item)
            .ToList();

        results.Add(new SearchItem(
            $"Web'de ara: “{query}”",
            SearchKind.Web,
            "https://www.google.com/search?q=" + Uri.EscapeDataString(query)));
        return results;
    }

    /// <summary>Karşılaştırma biçimi: Türkçe küçük harf, ı→i, aksanlar atılmış. Karakter sayısı korunur.</summary>
    internal static string Fold(string text)
    {
        var decomposed = text.ToLower(Turkish).Replace('ı', 'i').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static int Score(string name, string query)
    {
        var best = 0;
        for (var index = name.IndexOf(query, StringComparison.Ordinal);
             index >= 0;
             index = name.IndexOf(query, index + 1, StringComparison.Ordinal))
        {
            best = Math.Max(best, index == 0 ? 3 : IsWordStart(name, index) ? 2 : 1);
            if (best == 3)
            {
                break;
            }
        }

        return best;
    }

    private static bool IsWordStart(string name, int index) =>
        char.IsWhiteSpace(name[index - 1]) || name[index - 1] is '-' or '_' or '.' or '(' or '/' or '\\' or '"' or '“';
}
