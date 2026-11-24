using CamAda.Core.Record;

namespace CamAda.Core.Tests.Record;

public class TextureTests
{
    private const int Size = 256;
    private static readonly byte[] Disc = DiscTexture.Render(Size);
    private static readonly byte[] Sheen = SheenTexture.Render(Size);

    /// <summary>Merkezden r (yarıçapa oranla) uzakta, matematik açısı θ'da (derece, y yukarı) pikselin BGRA ofseti.</summary>
    private static int At(double r, double thetaDegrees)
    {
        var c = Size / 2.0;
        var t = thetaDegrees * Math.PI / 180;
        var x = (int)Math.Floor(c + r * c * Math.Cos(t));
        var y = (int)Math.Floor(c - r * c * Math.Sin(t));
        return (y * Size + x) * 4;
    }

    private static byte Alpha(byte[] pixels, int offset) => pixels[offset + 3];

    private static double Brightness(byte[] pixels, int offset) => (pixels[offset] + pixels[offset + 1] + pixels[offset + 2]) / 3.0;

    private static double RadialStdDev(byte[] pixels, double from, double to)
    {
        var values = new List<double>();
        for (var r = from; r <= to; r += 0.5 / (Size / 2.0))
        {
            values.Add(Brightness(pixels, At(r, 0)));
        }

        var mean = values.Average();
        return Math.Sqrt(values.Sum(v => (v - mean) * (v - mean)) / values.Count);
    }

    [Fact]
    public void Outside_the_disc_is_transparent() =>
        Assert.Equal(0, Alpha(Disc, 0)); // sol üst köşe

    [Fact]
    public void Groove_area_is_opaque() =>
        Assert.Equal(255, Alpha(Disc, At(0.7, 30)));

    [Fact]
    public void Label_area_is_left_transparent() =>
        Assert.Equal(0, Alpha(Disc, At(0.2, 30)));

    [Fact]
    public void Same_seed_gives_same_texture() =>
        Assert.Equal(DiscTexture.Render(64, seed: 3), DiscTexture.Render(64, seed: 3));

    [Fact]
    public void Different_seed_gives_different_grain() =>
        Assert.NotEqual(DiscTexture.Render(64, seed: 3), DiscTexture.Render(64, seed: 4));

    // Oluklar parlaklıkta ince dalgalanma yapar; giriş bölgesi (0.94–0.985) düzdür.
    [Fact]
    public void Grooves_ripple_more_than_the_flat_lead_in() =>
        Assert.True(RadialStdDev(Disc, 0.60, 0.70) > RadialStdDev(Disc, 0.945, 0.98),
            $"oluk {RadialStdDev(Disc, 0.60, 0.70):F2}, düz {RadialStdDev(Disc, 0.945, 0.98):F2}");

    [Fact]
    public void Sheen_is_bright_on_its_axis() =>
        Assert.True(Alpha(Sheen, At(0.7, 135)) > 40, $"{Alpha(Sheen, At(0.7, 135))}");

    [Fact]
    public void Sheen_has_a_mirror_wedge() =>
        Assert.True(Alpha(Sheen, At(0.7, 315)) > 40, $"{Alpha(Sheen, At(0.7, 315))}");

    [Fact]
    public void Sheen_is_absent_off_axis() =>
        Assert.Equal(0, Alpha(Sheen, At(0.7, 45)));
}
