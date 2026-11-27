using System.Globalization;

namespace CamAda.Core.Text;

/// <summary>Çekirdek metinlerini açık kültürle okur (gizli küresel durum yok; testler iki dili ayrı dener).</summary>
internal static class CoreText
{
    public static string Get(string key, CultureInfo ui) =>
        CoreStrings.ResourceManager.GetString(key, ui) ?? throw new InvalidOperationException("Eksik metin: " + key);

    public static string Format(CultureInfo ui, string key, params object?[] args) =>
        string.Format(ui, Get(key, ui), args);

    public static string Percent(int value, CultureInfo ui) => Format(ui, nameof(CoreStrings.Format_Percent), value);
}
