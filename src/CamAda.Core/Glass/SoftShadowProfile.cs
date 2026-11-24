namespace CamAda.Core.Glass;

/// <summary>Yumuşak gölgenin bir katmanı: şeklin kenarından <see cref="Outset"/> DIP dışarı (eksi: içeri) taşan dolgu.</summary>
public readonly record struct ShadowLayer(double Outset, double Alpha);

/// <summary>
/// Bulanıklık efekti kullanmadan yumuşak gölge: şeklin iç içe büyütülmüş kopyaları, dıştan içe doğru, Gauss bulanıklığının
/// kenar profilini (σ = yarıçap / 3) veren saydamlıklarla üst üste çizilir. Canlı bir Effect, altında ya da üstünde dönen bir
/// şey varken her karede yeniden hesaplanıyordu (ölçümde 30 kare/sn'de +%5 işlemci); düz dolgular bunun maliyetsiz karşılığı.
/// </summary>
public static class SoftShadowProfile
{
    /// <summary>
    /// En dıştan (kenardan blurRadius dışarı) en içe (blurRadius / 2 içeri) eşit aralıklı katmanlar. Bir noktadaki toplam
    /// saydamlık, onu kapsayan katmanların birleşimidir: kenarda yaklaşık opacity / 2, en içte tam opacity.
    /// </summary>
    public static IReadOnlyList<ShadowLayer> Layers(double blurRadius, double opacity, int layerCount)
    {
        var sigma = Math.Max(blurRadius / 3, 0.001);
        var outer = blurRadius;
        var inner = -blurRadius / 2;
        var layers = new List<ShadowLayer>(layerCount);
        var previous = 0.0;

        for (var k = 0; k < layerCount; k++)
        {
            var outset = layerCount == 1 ? inner : outer + (inner - outer) * k / (layerCount - 1);
            var target = k == layerCount - 1 ? opacity : opacity * NormalCdf(-outset / sigma);
            target = Math.Max(target, previous);

            // Bu katmanla birlikte toplam saydamlık target olsun: 1 − target = (1 − previous)(1 − alpha).
            var alpha = previous >= 1 ? 0 : 1 - (1 - target) / (1 - previous);
            layers.Add(new ShadowLayer(outset, Math.Clamp(alpha, 0, 1)));
            previous = target;
        }

        return layers;
    }

    /// <summary>Standart normal dağılımın birikimli fonksiyonu (Abramowitz–Stegun 7.1.26, hata &lt; 1,5e-7).</summary>
    private static double NormalCdf(double x)
    {
        var t = 1 / (1 + 0.3275911 * Math.Abs(x) / Math.Sqrt(2));
        var poly = t * (0.254829592 + t * (-0.284496736 + t * (1.421413741 + t * (-1.453152027 + t * 1.061405429))));
        var erf = 1 - poly * Math.Exp(-x * x / 2);
        return x >= 0 ? (1 + erf) / 2 : (1 - erf) / 2;
    }
}
