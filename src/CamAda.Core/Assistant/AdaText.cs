using System.Globalization;
using CamAda.Core.Text;
namespace CamAda.Core.Assistant;

/// <summary>Adadaki tek satır: uzun konuşmanın başı değil son cümlesi görünür (tasarım "son söylenen cümle").</summary>
public static class AdaText
{
    private static readonly char[] Ends = ['.', '!', '?', '…'];
    private static readonly char[] Closers = [' ', '"', '\'', '”', '’', '»', ')'];

    /// <summary>Dinlerken adadaki satır; misafir kipinde "Ada" demek gerektiğini söyler.</summary>
    public static string ListeningLine(bool guest, CultureInfo ui) =>
        CoreText.Get(guest ? nameof(CoreStrings.Ada_ListeningGuest) : nameof(CoreStrings.Ada_Listening), ui);

    /// <summary>Ada'nın son sözü soru mu (sondaki boşluk ve tırnaklar sayılmaz): ardından gelen yanıt adsız geçer.</summary>
    public static bool EndsWithQuestion(string text) => text.TrimEnd(Closers) is { Length: > 0 } t && t[^1] == '?';

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
