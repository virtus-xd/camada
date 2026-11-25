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
}
