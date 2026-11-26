using System.Globalization;

namespace CamAda.Core.Assistant;

/// <summary>
/// "Hey Ada" anahtar sözcüğü: sherpa-onnx gigaspeech KWS modelinin BPE birimleriyle (eğitimsiz; Plan 5 denemesinde
/// "Hey Ada" ve "Hey Eyda" söyleyişleri yakalandı). Satır biçimi: birimler, ":güçlendirme", "#eşik", "@etiket".
/// </summary>
public static class WakeWord
{
    public const string Tokens = "▁HE Y ▁A D A";
    public const string Label = "HEY_ADA";
    public const double Boost = 1.2;

    public static string KeywordLine(double threshold) =>
        string.Create(CultureInfo.InvariantCulture, $"{Tokens} :{Boost} #{threshold} @{Label}");

    /// <summary>Misafir kipinde oturum içinde aranan "Ada" (Plan 8 ölçümü: eşik 0,07, güç 1,5 ile 12 kümenin 10'u; yanlış tetiklenme 0,10 ile aynı).</summary>
    public const string NameTokens = "▁A D A";
    public const string NameLabel = "ADA";
    public const double NameThreshold = 0.07;
    public const double NameBoost = 1.5;

    public static string NameKeywordLine(double threshold, double boost) =>
        string.Create(CultureInfo.InvariantCulture, $"{NameTokens} :{boost} #{threshold} @{NameLabel}");
}
