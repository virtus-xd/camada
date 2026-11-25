#if DEBUG
using System.IO;
using System.Windows.Media.Imaging;
using CamAda.Clips;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// Anında ekran görüntüsü: sanal ekranı çeker; yalnız boyutu ve görüntünün boş olmadığını yazar (içerik günlüğe girmez).
    /// Var olmayan bir geçici alt klasöre aynı saniyede iki kez kaydeder, sonra klasörü siler. Panoya dokunmaz.
    /// </summary>
    private static void ScreenProbe()
    {
        var screen = ScreenCapture.VirtualScreen();
        var image = ScreenCapture.CaptureAllScreens();
        if (image is null)
        {
            Log($"ada-ekran-sina: sanal ekran {screen}; çekilemedi");
            return;
        }

        var stride = image.PixelWidth * 4;
        var pixels = new byte[stride * image.PixelHeight];
        new FormatConvertedBitmap(image, System.Windows.Media.PixelFormats.Bgra32, null, 0).CopyPixels(pixels, stride, 0);
        var first = BitConverter.ToInt32(pixels, 0);
        var varied = false;
        for (var i = 4; i < pixels.Length && !varied; i += 4 * 97)
        {
            varied = BitConverter.ToInt32(pixels, i) != first;
        }

        Log($"ada-ekran-sina: sanal ekran {screen}; görüntü {image.PixelWidth}×{image.PixelHeight}; {(varied ? "boş değil" : "TEK RENK")}");

        var folder = Path.Combine(Path.GetTempPath(), "camada-ekran-" + Guid.NewGuid().ToString("N")[..8], "Screenshots");
        try
        {
            var now = DateTime.Now;
            var a = ScreenCapture.Save(image, folder, now);
            var b = ScreenCapture.Save(image, folder, now);
            Log($"kaydedildi: {Path.GetFileName(a)} ({new FileInfo(a).Length} bayt), {Path.GetFileName(b)} ({new FileInfo(b).Length} bayt)");
        }
        finally
        {
            Directory.Delete(Path.GetDirectoryName(folder)!, recursive: true);
            Log($"geçici klasör silindi: {!Directory.Exists(folder)}");
        }
    }
}
#endif
