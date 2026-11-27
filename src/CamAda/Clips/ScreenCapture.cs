using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using CamAda.Core.Clipboard;

namespace CamAda.Clips;

/// <summary>
/// Tüm monitörlerin anında ekran görüntüsü (Win+PrtScn gibi, tuş göndermeden): sanal ekran GDI ile çekilir (uygulama
/// PerMonitorV2 DPI farkında: fiziksel piksel) ve PNG kaydedilir. Ada'nın "ekran görüntüsü al" komutu kullanır.
/// </summary>
internal static partial class ScreenCapture
{
    private const int SmXVirtualScreen = 76;
    private const int SmYVirtualScreen = 77;
    private const int SmCxVirtualScreen = 78;
    private const int SmCyVirtualScreen = 79;
    private const uint SrcCopy = 0x00CC0020;
    private const uint CaptureBlt = 0x40000000; // katmanlı pencereler (ada) de görüntüye girsin

    /// <summary>Bütün monitörleri kapsayan dikdörtgen (birincil monitörün solundaki monitörde X eksi olur).</summary>
    public static (int X, int Y, int Width, int Height) VirtualScreen() =>
        (GetSystemMetrics(SmXVirtualScreen), GetSystemMetrics(SmYVirtualScreen),
         GetSystemMetrics(SmCxVirtualScreen), GetSystemMetrics(SmCyVirtualScreen));

    /// <summary>Sanal ekranın görüntüsü (dondurulmuş, her iş parçacığında kullanılabilir); çekilemezse null.</summary>
    public static BitmapSource? CaptureAllScreens()
    {
        var (x, y, width, height) = VirtualScreen();
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var screen = GetDC(0);
        if (screen == 0)
        {
            return null;
        }

        nint memory = 0, bitmap = 0, previous = 0;
        try
        {
            memory = CreateCompatibleDC(screen);
            bitmap = CreateCompatibleBitmap(screen, width, height);
            if (memory == 0 || bitmap == 0)
            {
                return null;
            }

            previous = SelectObject(memory, bitmap);
            var copied = BitBlt(memory, 0, 0, width, height, screen, x, y, SrcCopy | CaptureBlt);
            SelectObject(memory, previous);
            previous = 0;
            if (!copied)
            {
                return null; // ör. güvenli masaüstü (UAC, kilit ekranı)
            }

            return FromHBitmap(bitmap);
        }
        catch (Exception ex) when (ex is ExternalException or ArgumentException or OutOfMemoryException)
        {
            return null;
        }
        finally
        {
            if (previous != 0)
            {
                SelectObject(memory, previous);
            }

            if (bitmap != 0)
            {
                DeleteObject(bitmap);
            }

            if (memory != 0)
            {
                DeleteDC(memory);
            }

            ReleaseDC(0, screen);
        }
    }

    /// <summary>
    /// GDI bitmap'inden dondurulmuş, opak WPF görüntüsü. GDI 32 bitlik piksellerin saydamlık baytını kullanmaz; bazı sürücülerde ya
    /// da uzak masaüstünde 0 gelir ve görüntü tamamen saydam kaydedilirdi (son inceleme, deneme: Bgra32 → PNG saydamlık 0). Bgr32'ye
    /// çevrilir: saydamlık yok sayılır (PNG de küçülür).
    /// </summary>
    internal static BitmapSource FromHBitmap(nint bitmap)
    {
        var raw = Imaging.CreateBitmapSourceFromHBitmap(bitmap, 0, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
        var image = new FormatConvertedBitmap(raw, System.Windows.Media.PixelFormats.Bgr32, null, 0);
        image.Freeze();
        return image;
    }

    /// <summary>
    /// Klasörü (yoksa) oluşturur ve görüntüyü "Ekran görüntüsü 2026-11-26 021530.png" adıyla kaydeder; aynı ad varsa " (2)"….
    /// Dosyanın yolunu döner; yazılamazsa <see cref="IOException"/> ya da <see cref="UnauthorizedAccessException"/>.
    /// </summary>
    public static string Save(BitmapSource image, string folder, DateTime now)
    {
        Directory.CreateDirectory(folder);
        var name = ScreenshotName.For(now, System.Globalization.CultureInfo.CurrentUICulture);
        var stem = Path.GetFileNameWithoutExtension(name);
        var path = Path.Combine(folder, name);
        for (var i = 2; File.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{stem} ({i}).png");
        }

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write))
        {
            encoder.Save(stream);
        }

        return path;
    }

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleBitmap(nint dc, int width, int height);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint gdiObject);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool BitBlt(nint destination, int x, int y, int width, int height, nint source, int sourceX, int sourceY, uint rop);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint gdiObject);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);
}
