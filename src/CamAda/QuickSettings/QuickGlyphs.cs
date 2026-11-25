using CamAda.Core.Network;
using CamAda.Core.Power;

namespace CamAda.QuickSettings;

/// <summary>
/// Hızlı ayarların Segoe Fluent Icons glifleri (24.11.2026'da yazı tipinde var oldukları ve doğru çizildikleri
/// denetlendi).
/// </summary>
public static class QuickGlyphs
{
    public const string Warning = "";
    public const string Cross = "";
    public const string Ethernet = "";
    public const string WifiFull = "";
    public const string Bluetooth = "";
    public const string Focus = "";
    public const string Theme = "";
    public const string Brightness = "";
    public const string Back = "";
    public const string Chevron = "";
    public const string Lock = "";
    public const string Reveal = "";

    /// <summary>Ana glif, soluk mu, sağ üst rozet (uyarı ya da çarpı).</summary>
    public static (string Glyph, bool Dim, string? Badge) Network(NetworkIcon icon) => icon.Kind switch
    {
        NetworkIconKind.Ethernet => (Ethernet, false, icon.Warning ? Warning : null),
        NetworkIconKind.Wifi => (Bars(icon.Bars), false, icon.Warning ? Warning : null),
        NetworkIconKind.WifiDisconnected => (WifiFull, true, null),
        NetworkIconKind.WifiOff => (WifiFull, true, Cross),
        _ => (Ethernet, true, Cross),
    };

    /// <summary>1–4 Wi‑Fi çubuğu.</summary>
    public static string Bars(int bars) => bars switch
    {
        <= 1 => "",
        2 => "",
        3 => "",
        _ => WifiFull,
    };

    /// <summary>
    /// 0–10 doluluk; şarjda ya da prize takılıyken şimşekli (fontta fiş glifi yok; "prize takılı, şarj etmiyor" ayrımını
    /// erişilebilir ad yapar).
    /// </summary>
    public static string Battery(BatteryIcon icon) => icon.Charging || icon.PluggedIn
        ? icon.Level switch
        {
            <= 8 => ((char)(0xE85A + Math.Max(0, icon.Level))).ToString(),
            9 => "",
            _ => "",
        }
        : icon.Level switch
        {
            <= 9 => ((char)(0xE850 + Math.Max(0, icon.Level))).ToString(),
            _ => "",
        };
}
