using System.Globalization;
using CamAda.Core.Text;

namespace CamAda.Core.Clipboard;

public static class ScreenshotName
{
    /// <summary>Ada'nın aldığı ekran görüntüsünün dosya adı, arayüz dilinde: "Ekran görüntüsü 2026-11-26 021530.png".</summary>
    public static string For(DateTime now, CultureInfo ui) =>
        CoreText.Format(ui, nameof(CoreStrings.Screenshot_FileName), now.ToString("yyyy-MM-dd HHmmss", CultureInfo.InvariantCulture));
}
