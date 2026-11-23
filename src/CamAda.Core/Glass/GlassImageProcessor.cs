namespace CamAda.Core.Glass;

/// <summary>Cam görünümü için duvar kağıdı kesitini bulanıklaştırır ve doygunluğunu artırır (BGRA, alfa korunur).</summary>
public static class GlassImageProcessor
{
    private const int Passes = 3; // 3 kutu bulanıklığı ≈ Gauss

    public static void Process(Span<byte> bgra, int width, int height, int blurRadius, double saturation)
    {
        if (blurRadius > 0)
        {
            var temp = new byte[bgra.Length];
            for (var pass = 0; pass < Passes; pass++)
            {
                BoxBlur(bgra, temp, width, height, blurRadius, horizontal: true);
                BoxBlur(temp, bgra, width, height, blurRadius, horizontal: false);
            }
        }

        if (saturation != 1.0)
        {
            Saturate(bgra, saturation);
        }
    }

    /// <summary>Tek yönde kayan pencereli kutu bulanıklığı; kenarda en yakın piksel tekrarlanır.</summary>
    private static void BoxBlur(ReadOnlySpan<byte> src, Span<byte> dst, int width, int height, int radius, bool horizontal)
    {
        var lines = horizontal ? height : width;
        var length = horizontal ? width : height;
        var window = 2 * radius + 1;
        Span<int> sums = stackalloc int[3];

        for (var line = 0; line < lines; line++)
        {
            int Index(int i)
            {
                var clamped = Math.Clamp(i, 0, length - 1);
                return (horizontal ? line * width + clamped : clamped * width + line) * 4;
            }

            sums.Clear();
            for (var i = -radius; i <= radius; i++)
            {
                var p = Index(i);
                sums[0] += src[p];
                sums[1] += src[p + 1];
                sums[2] += src[p + 2];
            }

            for (var i = 0; i < length; i++)
            {
                var o = Index(i);
                dst[o] = (byte)((sums[0] + window / 2) / window);
                dst[o + 1] = (byte)((sums[1] + window / 2) / window);
                dst[o + 2] = (byte)((sums[2] + window / 2) / window);
                dst[o + 3] = src[o + 3];

                var add = Index(i + radius + 1);
                var remove = Index(i - radius);
                sums[0] += src[add] - src[remove];
                sums[1] += src[add + 1] - src[remove + 1];
                sums[2] += src[add + 2] - src[remove + 2];
            }
        }
    }

    /// <summary>Doygunluk: L = 0.2126R + 0.7152G + 0.0722B; c' = L + (c - L) × çarpan, 0–255 aralığında.</summary>
    private static void Saturate(Span<byte> bgra, double factor)
    {
        for (var p = 0; p < bgra.Length; p += 4)
        {
            double b = bgra[p], g = bgra[p + 1], r = bgra[p + 2];
            var luminance = 0.2126 * r + 0.7152 * g + 0.0722 * b;
            bgra[p] = ToByte(luminance + (b - luminance) * factor);
            bgra[p + 1] = ToByte(luminance + (g - luminance) * factor);
            bgra[p + 2] = ToByte(luminance + (r - luminance) * factor);
        }
    }

    private static byte ToByte(double value) => (byte)Math.Clamp(Math.Round(value), 0, 255);
}
