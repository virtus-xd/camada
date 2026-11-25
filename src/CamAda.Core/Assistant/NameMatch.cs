using System.Globalization;

namespace CamAda.Core.Assistant;

public abstract record NameMatchResult
{
    private NameMatchResult()
    {
    }

    public sealed record Found(int Index) : NameMatchResult;

    public sealed record Ambiguous(IReadOnlyList<int> Indexes) : NameMatchResult
    {
        public bool Equals(Ambiguous? other) => other is not null && Indexes.SequenceEqual(other.Indexes);

        public override int GetHashCode() => Indexes.Count;
    }

    public sealed record NotFound : NameMatchResult;
}

/// <summary>
/// Söylenen adı (uygulama, cihaz, kişi) adaylarla eşler. Aşamalar sırayla: tam eşleşme, ad şununla başlıyor, bir sözcüğü
/// şununla başlıyor, içeriyor. Karşılaştırma tr-TR'de büyük/küçük harfe duyarsız (ı ≠ i). İlk sonuç veren aşamada farklı
/// adlı birden çok aday varsa belirsizdir.
/// </summary>
public static class NameMatch
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static NameMatchResult Best(string spoken, IReadOnlyList<string> candidates)
    {
        var query = spoken.Trim().ToLower(Turkish);
        if (query.Length == 0)
        {
            return new NameMatchResult.NotFound();
        }

        var lowered = candidates.Select(c => c.Trim().ToLower(Turkish)).ToList();
        Func<string, bool>[] stages =
        [
            c => c == query,
            c => c.StartsWith(query, StringComparison.Ordinal),
            c => WordStartIndex(c, query) >= 0,
            c => c.Contains(query, StringComparison.Ordinal),
        ];

        foreach (var stage in stages)
        {
            var matches = Enumerable.Range(0, lowered.Count).Where(i => stage(lowered[i])).ToList();
            if (matches.Count == 0)
            {
                continue;
            }

            return matches.Select(i => lowered[i]).Distinct().Count() == 1
                ? new NameMatchResult.Found(matches[0])
                : new NameMatchResult.Ambiguous(matches);
        }

        return new NameMatchResult.NotFound();
    }

    private static int WordStartIndex(string text, string query)
    {
        for (var i = text.IndexOf(query, StringComparison.Ordinal); i >= 0; i = text.IndexOf(query, i + 1, StringComparison.Ordinal))
        {
            if (i == 0 || !char.IsLetterOrDigit(text[i - 1]))
            {
                return i;
            }
        }

        return -1;
    }
}
