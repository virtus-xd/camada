using System.Globalization;
using CamAda.Core.Text;

namespace CamAda.Core.Time;

/// <summary>Adadaki saat ve tarih metinleri.</summary>
public static class ClockFormatter
{
    /// <summary>
    /// Türkçe arayüzde her zaman 24 saat ("09:05"); diğer dillerde Windows'un bölge ayarındaki kısa saat deseni
    /// (ABD "2:12 PM", İngiltere "14:12"). Kullanıcının kendi Windows'u Türkçe + ABD bölgesiyle çalışıyor; saati değişmesin.
    /// </summary>
    public static string Time(DateTime local, CultureInfo ui, CultureInfo region) =>
        UiLanguage.IsTurkish(ui) ? local.ToString("HH:mm", ui) : local.ToString("t", region);

    /// <summary>Kısa gün ve ay adı, arayüz dilinde: "Pzt, 23 Kasım" / "Mon, November 23".</summary>
    public static string Date(DateTime local, CultureInfo ui) =>
        local.ToString(CoreText.Get(nameof(CoreStrings.Format_IslandDate), ui), ui);
}
