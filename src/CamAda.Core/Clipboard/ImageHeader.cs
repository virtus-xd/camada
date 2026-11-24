using System.Buffers.Binary;

namespace CamAda.Core.Clipboard;

/// <summary>
/// Görselin boyutunu pikselleri açmadan üst bilgiden okur: panoya küçük ama devasa boyutlu bir görsel (sıkıştırma
/// bombası) gelirse, belleğe açılmadan önce reddedilebilsin.
/// </summary>
public static class ImageHeader
{
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>PNG'nin IHDR'sindeki genişlik ve yükseklik; PNG değilse null.</summary>
    public static (int Width, int Height)? PngSize(ReadOnlySpan<byte> png)
    {
        if (png.Length < 24 || !png[..8].SequenceEqual(PngSignature) || !png.Slice(12, 4).SequenceEqual("IHDR"u8))
        {
            return null;
        }

        var width = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(16, 4));
        var height = BinaryPrimitives.ReadUInt32BigEndian(png.Slice(20, 4));
        return width > int.MaxValue || height > int.MaxValue ? (int.MaxValue, int.MaxValue) : ((int)width, (int)height);
    }

    /// <summary>CF_DIB'in BITMAPINFOHEADER'ındaki boyut (yukarıdan aşağı DIB'de yükseklik eksidir); kısaysa null.</summary>
    public static (int Width, int Height)? DibSize(ReadOnlySpan<byte> dib)
    {
        if (dib.Length < 12)
        {
            return null;
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(dib.Slice(4, 4));
        var height = BinaryPrimitives.ReadInt32LittleEndian(dib.Slice(8, 4));
        return (Math.Abs(width), height == int.MinValue ? int.MaxValue : Math.Abs(height));
    }

    /// <summary>Piksel sayısı; boyut bilinmiyorsa en büyük değer (reddedilsin diye).</summary>
    public static long PixelCount((int Width, int Height)? size) =>
        size is { } s ? (long)s.Width * s.Height : long.MaxValue;
}
