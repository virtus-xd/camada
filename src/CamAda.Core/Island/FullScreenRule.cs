using CamAda.Core.Glass;

namespace CamAda.Core.Island;

/// <summary>ManagedShell'in tam ekran saydığı bir pencerenin anlık özeti (koordinatlar fiziksel piksel).</summary>
/// <param name="EffectiveBounds">Başlık + kalın kenarlı pencerede istemci alanı, diğerlerinde pencere dikdörtgeni.</param>
/// <param name="ScreenBounds">Pencerenin bulunduğu monitörün sınırları.</param>
/// <param name="ReportedByShell">Windows'un kendisi tam ekrana girdiğini bildirdi (sınır karşılaştırması gerekmez).</param>
public readonly record struct FullScreenCandidate(
    RectD EffectiveBounds,
    RectD ScreenBounds,
    bool Visible,
    bool Minimized,
    bool Maximized,
    bool HasCaption,
    bool ReportedByShell);

/// <summary>Bir pencerenin adayı tamamen gizleyecek kadar "tam ekran" olup olmadığına karar verir.</summary>
public static class FullScreenRule
{
    /// <summary>
    /// Büyütülmüş, başlık stilli pencere tam ekran değildir: Windows görev çubuğu gizliyken çalışma alanı tüm monitördür
    /// ve özel başlık çubuklu uygulamaların (VS Code, Chrome, Discord…) istemci alanı monitöre eşitlenir. Bunlar adayı
    /// gizlememeli, akıllı gizlenmeyle çizgiye indirmelidir. Gerçek tam ekranda (F11, oyun, video) pencere büyütülmüş
    /// değildir ya da başlık stili kaldırılmıştır.
    /// </summary>
    public static bool IsFullScreen(FullScreenCandidate c)
    {
        if (!c.Visible || c.Minimized || (c.Maximized && c.HasCaption))
        {
            return false;
        }

        return c.ReportedByShell || c.EffectiveBounds == c.ScreenBounds;
    }
}
