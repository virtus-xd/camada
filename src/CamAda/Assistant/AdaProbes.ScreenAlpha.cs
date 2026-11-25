#if DEBUG
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CamAda.Clips;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// Saydamlık: GDI ile boyanmış bir bellek resminin saydamlık baytı 0'dır (GDI o baytı kullanmaz). Bazı sürücülerde ekrandan
    /// okunan görüntü de böyle gelir. Çekim yolundan (<see cref="ScreenCapture.FromHBitmap"/>) geçip PNG'ye kaydedilen görüntü opak
    /// olmalı. Ekran okunmaz; yalnız bellekte 8×8 resim.
    /// </summary>
    private static void ScreenAlphaProbe()
    {
        var screen = GetDC(0);
        var memory = CreateCompatibleDC(screen);
        var bitmap = CreateCompatibleBitmap(screen, 8, 8);
        var previous = SelectObject(memory, bitmap);
        var brush = CreateSolidBrush(0x00336699);
        var rect = new Rect8 { Right = 8, Bottom = 8 };
        FillRect(memory, ref rect, brush);
        SelectObject(memory, previous);
        try
        {
            var image = ScreenCapture.FromHBitmap(bitmap);
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(image));
            using var png = new MemoryStream();
            encoder.Save(png);
            png.Position = 0;
            var decoded = new FormatConvertedBitmap(BitmapFrame.Create(png, BitmapCreateOptions.None, BitmapCacheOption.OnLoad), PixelFormats.Bgra32, null, 0);
            var pixels = new byte[8 * 8 * 4];
            decoded.CopyPixels(pixels, 8 * 4, 0);
            var alpha = Enumerable.Range(0, 64).Select(i => pixels[i * 4 + 3]).Distinct().ToList();
            Log($"ada-ekran-saydam-sina: biçim {image.Format}; PNG'deki saydamlık değerleri [{string.Join(",", alpha)}] → {(alpha.All(a => a == 255) ? "OPAK" : "SAYDAM")}");
        }
        finally
        {
            DeleteObject(brush);
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(0, screen);
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect8
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [LibraryImport("user32.dll")]
    private static partial nint GetDC(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial int ReleaseDC(nint hwnd, nint dc);

    [LibraryImport("user32.dll")]
    private static partial int FillRect(nint dc, ref Rect8 rect, nint brush);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleDC(nint dc);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateCompatibleBitmap(nint dc, int width, int height);

    [LibraryImport("gdi32.dll")]
    private static partial nint SelectObject(nint dc, nint gdiObject);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateSolidBrush(uint color);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteObject(nint gdiObject);

    [LibraryImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeleteDC(nint dc);
}
#endif
