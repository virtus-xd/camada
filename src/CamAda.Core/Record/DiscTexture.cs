namespace CamAda.Core.Record;

/// <summary>
/// Plağın dönen diskinin dokusu (spec §7.8 "Gerçekçilik"): kare BGRA32, önceden çarpılmış alfa. Yarıçapa (r / R) göre
/// bölgeler: 0,985–1 hafif kabarık, daha açık kenar; 0,94–0,985 düz giriş bölgesi; 0,40–0,94 altı parçalı oluklu alan
/// (parçalar arasında 0,006 genişliğinde düz bantlar); 0,36–0,40 düz çıkış bölgesi; r &lt; 0,36 saydam (etiket ayrı katman).
/// Oluklar yarıçapa bağlı ince parlaklık dalgalanmasıdır (periyot ~1,5 px); her yerde tohumlu ince gren (toz) vardır.
/// Aynı boyut ve tohum aynı baytları üretir. Doku dönerken yansıma ayrı, dönmeyen katmandadır (<see cref="SheenTexture"/>).
/// </summary>
public static class DiscTexture
{
    private const double LabelEdge = 0.36;
    private const double LeadOutEnd = 0.40;
    private const double GroovesEnd = 0.94;
    private const double LeadInEnd = 0.985;
    private const int Tracks = 6;
    private const double BandWidth = 0.006;
    private const double GroovePeriodPx = 1.5;
    private const double GrooveAmplitude = 6;
    private const double GrainAmplitude = 4;
    private const double BaseR = 0x0D, BaseG = 0x0D, BaseB = 0x10;

    public static byte[] Render(int size, int seed = 7)
    {
        var pixels = new byte[size * size * 4];
        var random = new Random(seed);
        var radius = size / 2.0;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var grain = (random.NextDouble() * 2 - 1) * GrainAmplitude; // her piksel için sırayla: tohum belirleyici
                var dx = x + 0.5 - radius;
                var dy = y + 0.5 - radius;
                var rPx = Math.Sqrt(dx * dx + dy * dy);
                var r = rPx / radius;

                // Kenar yumuşatma: dış kenarda ve etiket sınırında yarım piksellik geçiş.
                var alpha = Math.Clamp(radius - rPx + 0.5, 0, 1) * Math.Clamp(rPx - LabelEdge * radius + 0.5, 0, 1);
                if (alpha <= 0)
                {
                    continue;
                }

                var light = grain + Shade(r, rPx);
                var i = (y * size + x) * 4;
                pixels[i] = Premultiply(BaseB + light, alpha);
                pixels[i + 1] = Premultiply(BaseG + light, alpha);
                pixels[i + 2] = Premultiply(BaseR + light, alpha);
                pixels[i + 3] = (byte)Math.Round(alpha * 255);
            }
        }

        return pixels;
    }

    /// <summary>Bölgeye göre parlaklık eki: kabarık kenar daha açık, oluklu alanda ince dalgalanma, düz bölgeler sabit.</summary>
    private static double Shade(double r, double rPx)
    {
        if (r >= LeadInEnd)
        {
            return 14 + 6 * Math.Sin((r - LeadInEnd) / (1 - LeadInEnd) * Math.PI); // kabarık kenar
        }

        if (r >= GroovesEnd || r < LeadOutEnd)
        {
            return 2; // giriş ve çıkış bölgeleri: düz
        }

        if (IsTrackGap(r))
        {
            return 1; // şarkılar arası düz bant
        }

        return GrooveAmplitude * Math.Sin(2 * Math.PI * rPx / GroovePeriodPx);
    }

    private static bool IsTrackGap(double r)
    {
        var trackWidth = (GroovesEnd - LeadOutEnd) / Tracks;
        for (var k = 1; k < Tracks; k++)
        {
            var boundary = LeadOutEnd + k * trackWidth;
            if (Math.Abs(r - boundary) < BandWidth / 2)
            {
                return true;
            }
        }

        return false;
    }

    private static byte Premultiply(double channel, double alpha) =>
        (byte)Math.Round(Math.Clamp(channel, 0, 255) * alpha);
}

/// <summary>
/// Plağın dönmeyen ışık yansıması (spec §7.8): merkeze göre karşılıklı iki kama (matematik açısı, y yukarı: 135° ve 315°,
/// yarı açı 14°), yarıçapın 0,38–0,99 arasında. Kama ekseninde en parlak (alfa 0,22), kenarlarına yumuşak düşüş; ton
/// yarıçapa göre 0°'den 300°'ye kayan hafif gökkuşağı (doygunluk 0,25). Kama dışı tamamen saydam. BGRA32, önceden çarpılmış.
/// </summary>
public static class SheenTexture
{
    private const double InnerEdge = 0.38;
    private const double OuterEdge = 0.99;
    private const double HalfAngle = 14;
    private const double PeakAlpha = 0.22;
    private const double Saturation = 0.25;
    private static readonly double[] Axes = [135, 315];

    public static byte[] Render(int size)
    {
        var pixels = new byte[size * size * 4];
        var radius = size / 2.0;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var dx = x + 0.5 - radius;
                var dy = radius - (y + 0.5); // y yukarı
                var r = Math.Sqrt(dx * dx + dy * dy) / radius;
                if (r < InnerEdge || r > OuterEdge)
                {
                    continue;
                }

                var theta = Math.Atan2(dy, dx) * 180 / Math.PI;
                var delta = Axes.Min(axis => AngleBetween(theta, axis));
                if (delta >= HalfAngle)
                {
                    continue;
                }

                var across = 1 - delta / HalfAngle;                  // kama ekseninde 1, kenarında 0
                var along = SmoothStep(InnerEdge, InnerEdge + 0.06, r) * SmoothStep(OuterEdge, OuterEdge - 0.04, r);
                var alpha = PeakAlpha * across * across * (3 - 2 * across) * along;
                if (alpha <= 0)
                {
                    continue;
                }

                var hue = 300 * (r - InnerEdge) / (OuterEdge - InnerEdge);
                var (red, green, blue) = HsvToRgb(hue, Saturation, 1);
                var i = (y * size + x) * 4;
                pixels[i] = (byte)Math.Round(blue * alpha * 255);
                pixels[i + 1] = (byte)Math.Round(green * alpha * 255);
                pixels[i + 2] = (byte)Math.Round(red * alpha * 255);
                pixels[i + 3] = (byte)Math.Round(alpha * 255);
            }
        }

        return pixels;
    }

    private static double AngleBetween(double a, double b)
    {
        var d = Math.Abs(((a - b) % 360 + 540) % 360 - 180);
        return d;
    }

    private static double SmoothStep(double edge0, double edge1, double x)
    {
        var t = Math.Clamp((x - edge0) / (edge1 - edge0), 0, 1);
        return t * t * (3 - 2 * t);
    }

    private static (double R, double G, double B) HsvToRgb(double hue, double saturation, double value)
    {
        var c = value * saturation;
        var h = hue / 60 % 6;
        var x = c * (1 - Math.Abs(h % 2 - 1));
        var (r, g, b) = h switch
        {
            < 1 => (c, x, 0.0),
            < 2 => (x, c, 0.0),
            < 3 => (0.0, c, x),
            < 4 => (0.0, x, c),
            < 5 => (x, 0.0, c),
            _ => (c, 0.0, x),
        };
        var m = value - c;
        return (r + m, g + m, b + m);
    }
}
