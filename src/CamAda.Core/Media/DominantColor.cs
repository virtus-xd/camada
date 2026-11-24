namespace CamAda.Core.Media;

public readonly record struct Rgb(byte R, byte G, byte B);

/// <summary>Kapağın baskın rengi (plağın çevresindeki hafif hâle için, spec §7.8).</summary>
public static class DominantColor
{
    /// <summary>Kapak yoksa ya da renksizse kullanılan renk (spec §7.9, #5E4C86).</summary>
    public static readonly Rgb Fallback = new(0x5E, 0x4C, 0x86);

    private const int HueBuckets = 12;
    private const int MinAlpha = 128;
    private const int MinBrightness = 40;
    private const double MinSaturation = 0.15;

    /// <summary>
    /// BGRA32 piksellerden baskın renk: yarı saydam, çok koyu ve renksiz (gri) pikseller atlanır; kalanlar 30°'lik
    /// 12 ton kovasına ayrılır ve en kalabalık kovanın ortalaması döner. Uygun piksel yoksa <see cref="Fallback"/>.
    /// </summary>
    public static Rgb From(ReadOnlySpan<byte> bgra)
    {
        Span<long> count = stackalloc long[HueBuckets];
        Span<long> sumR = stackalloc long[HueBuckets];
        Span<long> sumG = stackalloc long[HueBuckets];
        Span<long> sumB = stackalloc long[HueBuckets];

        for (var i = 0; i + 3 < bgra.Length; i += 4)
        {
            int b = bgra[i], g = bgra[i + 1], r = bgra[i + 2], a = bgra[i + 3];
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            if (a < MinAlpha || max < MinBrightness || (max - min) / (double)max < MinSaturation)
            {
                continue;
            }

            var bucket = (int)(Hue(r, g, b, max, min) / 360.0 * HueBuckets) % HueBuckets;
            count[bucket]++;
            sumR[bucket] += r;
            sumG[bucket] += g;
            sumB[bucket] += b;
        }

        var best = -1;
        for (var k = 0; k < HueBuckets; k++)
        {
            if (count[k] > 0 && (best < 0 || count[k] > count[best]))
            {
                best = k;
            }
        }

        return best < 0
            ? Fallback
            : new Rgb((byte)(sumR[best] / count[best]), (byte)(sumG[best] / count[best]), (byte)(sumB[best] / count[best]));
    }

    private static double Hue(int r, int g, int b, int max, int min)
    {
        double delta = max - min;
        var hue = max == r ? (g - b) / delta % 6
            : max == g ? (b - r) / delta + 2
            : (r - g) / delta + 4;
        hue *= 60;
        return hue < 0 ? hue + 360 : hue;
    }
}
