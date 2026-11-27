using System.Globalization;

namespace CamAda.Core.Text;

/// <summary>
/// Arayüz dili: ayarda "tr"/"en" seçildiyse o, yoksa Windows'un görüntü dili Türkçe ise Türkçe, değilse İngilizce.
/// Saat ve sayı biçimi ayrıca Windows'un bölge ayarından gelir (bkz. Time.ClockFormatter).
/// </summary>
public static class UiLanguage
{
    public static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    public static readonly CultureInfo English = CultureInfo.GetCultureInfo("en-US");

    public static CultureInfo Resolve(string? setting, CultureInfo windowsUiCulture) => setting switch
    {
        "tr" => Turkish,
        "en" => English,
        _ => IsTurkish(windowsUiCulture) ? Turkish : English,
    };

    public static bool IsTurkish(CultureInfo culture) => culture.TwoLetterISOLanguageName == "tr";
}
