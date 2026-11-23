using CamAda.Core.Glass;

namespace CamAda.Core.Tests.Glass;

public class GlassImageProcessorTests
{
    private static byte[] Solid(int w, int h, byte b, byte g, byte r) =>
        Enumerable.Range(0, w * h).SelectMany(_ => new[] { b, g, r, (byte)255 }).ToArray();

    [Fact]
    public void Uniform_image_is_unchanged_by_blur()
    {
        var px = Solid(8, 8, 30, 20, 10);
        GlassImageProcessor.Process(px, 8, 8, blurRadius: 2, saturation: 1.0);
        Assert.Equal(Solid(8, 8, 30, 20, 10), px);
    }

    [Fact]
    public void Blur_spreads_a_bright_pixel()
    {
        var px = Solid(9, 9, 0, 0, 0);
        int c = (4 * 9 + 4) * 4;
        px[c] = px[c + 1] = px[c + 2] = 255;
        GlassImageProcessor.Process(px, 9, 9, blurRadius: 1, saturation: 1.0);
        Assert.InRange(px[c], 1, 254);
        Assert.True(px[c + 4] > 0); // sağ komşu aydınlandı
    }

    [Fact]
    public void Saturation_keeps_gray()
    {
        var px = Solid(1, 1, 128, 128, 128);
        GlassImageProcessor.Process(px, 1, 1, blurRadius: 0, saturation: 1.7);
        Assert.Equal(new byte[] { 128, 128, 128, 255 }, px);
    }

    [Fact]
    public void Saturation_pushes_color_away_from_gray_and_keeps_alpha()
    {
        var px = new byte[] { 200, 100, 50, 255 }; // B, G, R, A
        GlassImageProcessor.Process(px, 1, 1, blurRadius: 0, saturation: 2.0);
        Assert.Equal(new byte[] { 255, 103, 3, 255 }, px);
    }
}
