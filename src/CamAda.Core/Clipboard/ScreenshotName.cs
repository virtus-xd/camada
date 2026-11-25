using System.Globalization;

namespace CamAda.Core.Clipboard;

public static class ScreenshotName
{
    /// <summary>Ada'nın aldığı ekran görüntüsünün dosya adı: "Ekran görüntüsü 2026-11-26 021530.png".</summary>
    public static string For(DateTime now) => "Ekran görüntüsü " + now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture) + ".png";
}
