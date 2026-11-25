namespace CamAda.Core.Assistant;

/// <summary>Adadaki tek satır: uzun konuşmanın başı değil son cümlesi görünür (tasarım "son söylenen cümle").</summary>
public static class AdaText
{
    private static readonly char[] Ends = ['.', '!', '?', '…'];

    /// <summary>
    /// Son biten cümle (noktalamasıyla) ya da sürmekte olan son parça; <paramref name="max"/> karakteri aşarsa sonu
    /// kalır, başına "…" konur.
    /// </summary>
    public static string LastSentence(string text, int max)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return "";
        }

        // Sondaki noktalama dışında kalan son cümle sonu: ondan sonrası son cümledir.
        var searchEnd = trimmed.Length - 1;
        while (searchEnd >= 0 && Array.IndexOf(Ends, trimmed[searchEnd]) >= 0)
        {
            searchEnd--;
        }

        var start = searchEnd < 0 ? 0 : trimmed.LastIndexOfAny(Ends, searchEnd) + 1;
        var sentence = trimmed[start..].Trim();
        return sentence.Length <= max ? sentence : "…" + sentence[^(max - 1)..].TrimStart();
    }
}
