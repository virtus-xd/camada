namespace CamAda.Core.Clipboard;

/// <summary>Panodan neyin okunacağına dair kurallar (son inceleme düzeltmeleri).</summary>
public static class ClipboardPolicy
{
    /// <summary>
    /// HTML ve RTF yalnız bu boyuta kadar olan metinlerde istenir: Office bu biçimleri istek anında üretir; büyük bir
    /// Excel aralığında bu saniyeler sürer, bu sırada kabuk ve pano kilitlenir. Büyük kopyada yalnız düz metin saklanır.
    /// </summary>
    public const long MaxTextBytesForRichFormats = 1024 * 1024;

    public static bool ShouldReadRichFormats(long textBytes) => textBytes <= MaxTextBytesForRichFormats;

    /// <summary>
    /// Kopya geçmişe alınmaz mı: dışlama biçimleri varsa ya da <c>CanIncludeInClipboardHistory</c> 0 ise. İşaret var ama
    /// okunamıyorsa da alınmaz (şifre için güvenli taraf).
    /// </summary>
    public static bool IsExcluded(bool excludeFormat, bool viewerIgnore, bool canIncludePresent, byte[]? canIncludeValue) =>
        excludeFormat || viewerIgnore
        || (canIncludePresent && (canIncludeValue is not { Length: >= 4 } || BitConverter.ToInt32(canIncludeValue, 0) == 0));
}
