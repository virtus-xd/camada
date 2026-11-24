namespace CamAda.Core.Clipboard;

/// <summary>Pano öğesinin türü: panelde simgesini ve önizlemesini belirler.</summary>
public enum ClipKind
{
    Text,
    Link,
    Code,
    Image,
}

/// <summary>
/// Panodan okunan içerik: metin (biçimleriyle) ya da görsel. Yeniden kopyalanınca biçimler korunsun diye HTML ve RTF de
/// saklanır. Görselin aynılığı piksellerinden (<paramref name="PixelHash"/>) anlaşılır, PNG baytlarından değil.
/// </summary>
public sealed record ClipPayload(string? Text, string? Html, string? Rtf, byte[]? Png, int Width, int Height, string? PixelHash)
{
    public static ClipPayload FromText(string text, string? html = null, string? rtf = null) =>
        new(text, html, rtf, null, 0, 0, null);

    public static ClipPayload FromImage(byte[] png, int width, int height, string pixelHash) =>
        new(null, null, null, png, width, height, pixelHash);

    public bool IsImage => Png is not null;

    /// <summary>Aynı içeriğin anahtarı: metinde düz metin, görselde piksel özeti.</summary>
    public string Key => IsImage ? "i:" + PixelHash : "t:" + Text;

    /// <summary>Saklanan boyut (bayt): metinlerde karakter × 2, görselde PNG uzunluğu.</summary>
    public long Size => ((long)(Text?.Length ?? 0) + (Html?.Length ?? 0) + (Rtf?.Length ?? 0)) * 2 + (Png?.LongLength ?? 0);
}

/// <summary>Geçmişteki bir kopya.</summary>
public sealed record ClipItem(string Id, ClipKind Kind, ClipPayload Payload, DateTimeOffset CopiedAt, bool Pinned);
