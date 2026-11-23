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

/// <summary>Adanın yerini bir pencerenin kaplayıp kaplamadığına karar verir.</summary>
public static class OcclusionRule
{
    /// <summary>
    /// Z-sırasında yukarıdan aşağı, uygun ve yoklama noktasını içeren ilk pencereye bakar:
    /// masaüstüyse ada açıkta, başka bir pencereyse ada örtülü.
    /// </summary>
    public static bool IsOccluded(IEnumerable<WindowSample> topToBottom, double probeX, double probeY)
    {
        foreach (var window in topToBottom)
        {
            var eligible = window.Visible && !window.Cloaked && !window.Ignorable && !window.IsOwn
                           && (window.HasTitle || window.IsDesktop);
            if (!eligible || !Contains(window.Bounds, probeX, probeY))
            {
                continue;
            }

            return !window.IsDesktop;
        }

        return false;
    }

    private static bool Contains(RectD r, double x, double y) =>
        x >= r.X && x < r.X + r.Width && y >= r.Y && y < r.Y + r.Height;
}
