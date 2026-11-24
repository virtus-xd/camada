namespace CamAda.Core.Media;

/// <summary>Medya oturumunun kapağının (küçük resim) ne zaman yeniden okunacağı.</summary>
public static class CoverRule
{
    /// <summary>
    /// Parça değiştiyse okunur. Aynı parçada kapak henüz yoksa ve küçük resim artık varsa yine okunur: oturum önce başlığı,
    /// biraz sonra küçük resmi bildirebilir. Kapağı olan parça (ya da küçük resmi olmayan parça) yeniden okunmaz.
    /// </summary>
    public static bool ShouldLoad(string key, string? lastKey, bool lastHadCover, bool hasThumbnail) =>
        key != lastKey || (!lastHadCover && hasThumbnail);
}
