using CamAda.Core.Glass;

namespace CamAda.Core.Island;

/// <summary>Bir üst düzey pencerenin örtülme hesabı için gereken özeti (koordinatlar fiziksel piksel).</summary>
/// <param name="Ignorable">Araç penceresi, odak almayan ya da tıklamayı geçiren katman (ör. ipuçları, oyun üstü katmanlar).</param>
/// <param name="IsDesktop">Masaüstü penceresi (Progman / WorkerW).</param>
/// <param name="IsOwn">Bizim süreçimize ait pencere.</param>
public readonly record struct WindowSample(
    RectD Bounds,
    bool Visible,
    bool Cloaked,
    bool Ignorable,
    bool HasTitle,
    bool IsDesktop,
    bool IsOwn);

/// <summary>Adanın bir pencerenin üstünü kapatıp kapatmadığına karar verir.</summary>
public static class OcclusionRule
{
    /// <summary>
    /// Ada her zaman en üstte olduğu için, adanın kapladığı alanla (hap + gölge) kesişen her uygun pencere
    /// adanın altında kalır: başlık çubuğu ya da sekmeleri örtülür. Masaüstü sayılmaz.
    /// </summary>
    public static bool IsOccluded(IEnumerable<WindowSample> windows, RectD islandFootprint)
    {
        foreach (var window in windows)
        {
            var eligible = window.Visible && !window.Cloaked && !window.Ignorable && !window.IsOwn
                           && window.HasTitle && !window.IsDesktop;
            if (eligible && Intersects(window.Bounds, islandFootprint))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Intersects(RectD a, RectD b) =>
        a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height && b.Y < a.Y + a.Height;
}

/// <summary>Akıllı gizlenmede adayı açan üst kenar bölgesi.</summary>
public static class RevealZone
{
    /// <summary>
    /// İmleç ana ekranın en üst iki piksel satırında ve ada penceresinin genişliği içindeyse true.
    /// Ana ekranın üstündeki bir monitörde (y &lt; ekran üstü) tetiklenmez.
    /// </summary>
    public static bool IsAtTopEdge(int x, int y, double windowLeft, double windowRight, int screenTop) =>
        y >= screenTop && y <= screenTop + 1 && x >= windowLeft && x <= windowRight;
}
