using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CamAda.Core.Clipboard;

namespace CamAda.Clips;

/// <summary>
/// Panodan gelen görseli geçmişe uygun hâle getirir (arka plan iş parçacığında çalışır): PNG saklanır, aynılık BGRA
/// piksellerinin SHA-256 özetiyle anlaşılır. DIB, dosya üst bilgisi eklenerek BMP olarak çözülür.
/// </summary>
internal static class ClipImages
{
    private const long MaxPixels = 100_000_000;

    public static ClipPayload? Prepare(byte[]? png, byte[]? dib)
    {
        BitmapSource source;
        if (png is not null)
        {
            source = BitmapDecoder.Create(new MemoryStream(png), BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
        }
        else if (dib is { Length: >= 40 })
        {
            source = new BmpBitmapDecoder(new MemoryStream(WithFileHeader(dib)), BitmapCreateOptions.None, BitmapCacheOption.OnLoad).Frames[0];
        }
        else
        {
            return null;
        }

        var (width, height) = (source.PixelWidth, source.PixelHeight);
        if (width <= 0 || height <= 0 || (long)width * height > MaxPixels)
        {
            return null;
        }

        var hash = PixelHash(source);
        if (png is null)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));
            using var stream = new MemoryStream();
            encoder.Save(stream);
            png = stream.ToArray();
        }

        return ClipPayload.FromImage(png, width, height, hash);
    }

    /// <summary>Görselin BGRA piksellerinin özeti (aynı görüntünün farklı kodlamaları aynı özeti verir).</summary>
    public static string PixelHash(BitmapSource source)
    {
        var bgra = new FormatConvertedBitmap(source, PixelFormats.Bgra32, null, 0);
        var stride = bgra.PixelWidth * 4;
        var pixels = new byte[(long)stride * bgra.PixelHeight];
        bgra.CopyPixels(pixels, stride, 0);
        return Convert.ToHexString(SHA256.HashData(pixels));
    }

    /// <summary>CF_DIB baytlarının önüne BITMAPFILEHEADER ekler.</summary>
    private static byte[] WithFileHeader(byte[] dib)
    {
        var headerSize = BitConverter.ToInt32(dib, 0);
        var bitCount = BitConverter.ToInt16(dib, 14);
        var compression = BitConverter.ToInt32(dib, 16);
        var colorsUsed = headerSize >= 36 ? BitConverter.ToInt32(dib, 32) : 0;
        var colors = colorsUsed > 0 ? colorsUsed : bitCount <= 8 ? 1 << bitCount : 0;
        var masks = headerSize == 40 ? compression switch { 3 => 12, 6 => 16, _ => 0 } : 0;
        var offset = 14 + headerSize + masks + colors * 4;

        var file = new byte[14 + dib.Length];
        file[0] = (byte)'B';
        file[1] = (byte)'M';
        BitConverter.GetBytes(file.Length).CopyTo(file, 2);
        BitConverter.GetBytes(offset).CopyTo(file, 10);
        dib.CopyTo(file, 14);
        return file;
    }
}
