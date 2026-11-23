using CamAda.Core.Glass;

namespace CamAda.Core.Tests.Glass;

public class WallpaperTests
{
    [Theory]
    [InlineData("10", "0", WallpaperStyle.Fill)]
    [InlineData("6", "0", WallpaperStyle.Fit)]
    [InlineData("2", "0", WallpaperStyle.Stretch)]
    [InlineData("0", "0", WallpaperStyle.Center)]
    [InlineData("0", "1", WallpaperStyle.Fill)]
    [InlineData("22", "0", WallpaperStyle.Fill)]
    [InlineData(null, null, WallpaperStyle.Fill)]
    public void Parses_registry_style(string? style, string? tile, WallpaperStyle expected) =>
        Assert.Equal(expected, WallpaperStyleParser.Parse(style, tile));

    private static void Near(RectD expected, RectD actual)
    {
        Assert.Equal(expected.X, actual.X, 3);
        Assert.Equal(expected.Y, actual.Y, 3);
        Assert.Equal(expected.Width, actual.Width, 3);
        Assert.Equal(expected.Height, actual.Height, 3);
    }

    private static readonly SizeD Screen = new(1536, 864);

    [Fact]
    public void Fill_same_aspect() =>
        Near(new RectD(710, 10, 500, 50),
             WallpaperLayout.MapToImage(new RectD(568, 8, 400, 40), Screen, new SizeD(1920, 1080), WallpaperStyle.Fill));

    [Fact]
    public void Fill_wide_image_is_cropped_horizontally() =>
        Near(new RectD(1268.519, 0, 462.963, 64.815),
             WallpaperLayout.MapToImage(new RectD(568, 0, 400, 56), Screen, new SizeD(3000, 1000), WallpaperStyle.Fill));

    [Fact]
    public void Stretch_full_screen_maps_to_full_image() =>
        Near(new RectD(0, 0, 1000, 1000),
             WallpaperLayout.MapToImage(new RectD(0, 0, 1536, 864), Screen, new SizeD(1000, 1000), WallpaperStyle.Stretch));

    [Fact]
    public void Fit_region_left_of_image_is_outside() =>
        Near(new RectD(-388.889, 0, 115.741, 115.741),
             WallpaperLayout.MapToImage(new RectD(0, 0, 100, 100), Screen, new SizeD(1000, 1000), WallpaperStyle.Fit));

    [Fact]
    public void Center_unscaled() =>
        Near(new RectD(0, 0, 10, 10),
             WallpaperLayout.MapToImage(new RectD(268, 182, 10, 10), Screen, new SizeD(1000, 500), WallpaperStyle.Center));
}
