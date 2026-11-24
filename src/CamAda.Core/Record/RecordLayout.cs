using CamAda.Core.Glass;
using CamAda.Core.Island;
using CamAda.Core.Settings;

namespace CamAda.Core.Record;

/// <summary>Plak penceresinin yerleşimi (DIP): pencere ekran koordinatında; disk, görünen disk ve kart pencereye göre.</summary>
public readonly record struct RecordGeometry(RectD Window, RectD Disc, RectD VisibleDisc, RectD Card);

/// <summary>
/// Plak çaların yerleşimi (spec §7.8): ana ekranın sol kenarında, çapın bir kısmı ekran dışında, alt kenarı ekranın altından
/// yukarıda; müzik kartı görünen diskin sağında, dikeyde diskin merkezine ortalı.
/// </summary>
public static class RecordLayout
{
    public const double CardWidth = 300;
    public const double CardHeight = 196;
    public const double CardGap = 16;

    /// <summary>Gölge ve hâle için pencerenin sağ, üst ve alt payı.</summary>
    public const double ShadowPad = 40;

    /// <summary>
    /// Pencere ana ekranın sol kenarından başlar ve ekranın içinde kalır; diskin ekran dışı kısmı pencerede kırpılır. Böylece
    /// ana ekranın solunda ya da altında başka bir monitör olsa da plağın o kısmı orada görünmez.
    /// </summary>
    public static RecordGeometry Compute(RectD screen, RecordSettings settings)
    {
        var d = settings.Diameter;
        var discLeft = screen.X - settings.OutsideFraction * d;          // ekran koordinatı
        var discTop = screen.Y + screen.Height - settings.BottomOffset - d;
        var visibleRight = discLeft + d;
        var discCenterY = discTop + d / 2;

        var screenBottom = screen.Y + screen.Height;
        var cardLeft = visibleRight + CardGap;
        var cardTop = Math.Clamp(discCenterY - CardHeight / 2, screen.Y, screenBottom - CardHeight);

        var windowLeft = screen.X;
        var windowTop = Math.Max(screen.Y, Math.Min(discTop, cardTop) - ShadowPad);
        var windowRight = Math.Min(screen.X + screen.Width, cardLeft + CardWidth + ShadowPad);
        var windowBottom = Math.Min(screenBottom, Math.Max(discTop + d, cardTop + CardHeight) + ShadowPad);
        var window = new RectD(windowLeft, windowTop, windowRight - windowLeft, windowBottom - windowTop);

        RectD Local(double x, double y, double w, double h) => new(x - windowLeft, y - windowTop, w, h);
        return new RecordGeometry(
            window,
            Local(discLeft, discTop, d, d),
            Local(Math.Max(discLeft, screen.X), discTop, visibleRight - Math.Max(discLeft, screen.X), d),
            Local(cardLeft, cardTop, CardWidth, CardHeight));
    }
}

/// <summary>Plağın tıklanan ve üzerine gelinen alanı: dönen kare görüntünün tamamı değil, yalnız diskin dairesi.</summary>
public static class DiscHit
{
    public static bool Contains(double x, double y, double width, double height)
    {
        var radius = Math.Min(width, height) / 2;
        var dx = x - width / 2;
        var dy = y - height / 2;
        return radius > 0 && dx * dx + dy * dy <= radius * radius;
    }
}

/// <summary>Plak pencerelerin arkasında durduğu için tamamen örtülünce dönüş çizimi durdurulur (boşta işlemci harcamasın).</summary>
public static class RecordVisibility
{
    /// <summary>
    /// Görünen disk dikdörtgenini (ekran, fiziksel piksel) tamamen içine alan uygun bir pencere varsa true: görünür,
    /// gizlenmemiş, yok sayılmayan, başlıklı, masaüstü olmayan ve bizim olmayan. Pencereler z-sırasıyla (üstten alta)
    /// gelir; plağın kendi penceresinin altındakiler sayılmaz (Win+D masaüstünü ve plağı küçülmemiş pencerelerin önüne getirir).
    /// </summary>
    public static bool IsCovered(IEnumerable<WindowSample> windows, RectD visibleDiscOnScreen)
    {
        foreach (var w in windows)
        {
            if (w.IsSelf)
            {
                return false;
            }

            var eligible = w.Visible && !w.Cloaked && !w.Ignorable && w.HasTitle && !w.IsDesktop && !w.IsOwn;
            if (eligible && Contains(w.Bounds, visibleDiscOnScreen))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(RectD outer, RectD inner) =>
        inner.X >= outer.X && inner.Y >= outer.Y
        && inner.X + inner.Width <= outer.X + outer.Width
        && inner.Y + inner.Height <= outer.Y + outer.Height;
}
