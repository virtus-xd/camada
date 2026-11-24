namespace CamAda.Core.Theme;

/// <summary>
/// Koyu tema kutucuğu (Plan 4.3): Windows'un <c>Personalize</c> anahtarındaki <c>AppsUseLightTheme</c> 0 ise koyu;
/// değer yoksa Windows açık temadadır. Kutucuk hem uygulama hem Windows modunu aynı değerle yazar.
/// </summary>
public static class ThemeRule
{
    public static bool IsDark(int? appsUseLightTheme) => appsUseLightTheme == 0;

    public static int RegistryValue(bool dark) => dark ? 0 : 1;
}
