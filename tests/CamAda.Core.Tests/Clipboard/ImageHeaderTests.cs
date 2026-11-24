using CamAda.Core.Clipboard;

namespace CamAda.Core.Tests.Clipboard;

public class ImageHeaderTests
{
    private static byte[] Png(uint width, uint height)
    {
        var bytes = new byte[33];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        "IHDR"u8.ToArray().CopyTo(bytes, 12);
        bytes[16] = (byte)(width >> 24); bytes[17] = (byte)(width >> 16); bytes[18] = (byte)(width >> 8); bytes[19] = (byte)width;
        bytes[20] = (byte)(height >> 24); bytes[21] = (byte)(height >> 16); bytes[22] = (byte)(height >> 8); bytes[23] = (byte)height;
        return bytes;
    }

    private static byte[] Dib(int width, int height)
    {
        var bytes = new byte[40];
        BitConverter.GetBytes(40).CopyTo(bytes, 0);
        BitConverter.GetBytes(width).CopyTo(bytes, 4);
        BitConverter.GetBytes(height).CopyTo(bytes, 8);
        return bytes;
    }

    [Fact]
    public void Reads_png_size_from_ihdr() => Assert.Equal((1280, 720), ImageHeader.PngSize(Png(1280, 720)));

    [Fact]
    public void Huge_png_is_reported_not_decoded() => Assert.Equal(100_000L * 100_000, ImageHeader.PixelCount(ImageHeader.PngSize(Png(100_000, 100_000))));

    [Fact]
    public void Not_a_png_has_no_size()
    {
        Assert.Null(ImageHeader.PngSize([1, 2, 3]));
        var wrong = Png(10, 10);
        wrong[1] = 0;
        Assert.Null(ImageHeader.PngSize(wrong));
    }

    [Fact]
    public void Reads_dib_size_and_top_down_height_is_positive()
    {
        Assert.Equal((100, 60), ImageHeader.DibSize(Dib(100, 60)));
        Assert.Equal((100, 60), ImageHeader.DibSize(Dib(100, -60)));
    }

    [Fact]
    public void Short_dib_has_no_size() => Assert.Null(ImageHeader.DibSize(new byte[10]));

    [Fact]
    public void Missing_size_counts_as_too_big() => Assert.Equal(long.MaxValue, ImageHeader.PixelCount(null));
}
