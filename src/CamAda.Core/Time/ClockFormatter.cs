using System.Globalization;

namespace CamAda.Core.Time;

/// <summary>Adadaki saat ve tarih metinleri (tr-TR).</summary>
public static class ClockFormatter
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>24 saat biçimi, ör. "09:05".</summary>
    public static string Time(DateTime local) => local.ToString("HH:mm", Turkish);

    /// <summary>Kısa gün ve ay adı, ör. "Pzt, 23 Kasım".</summary>
    public static string Date(DateTime local) => local.ToString("ddd, d MMMM", Turkish);
}
