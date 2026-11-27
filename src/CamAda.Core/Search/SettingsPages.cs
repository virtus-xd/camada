using System.Globalization;
using CamAda.Core.Text;

namespace CamAda.Core.Search;

/// <summary>
/// Başlat panelinden aranabilen Windows Ayarlar sayfaları. Ad arayüz dilinde görünür; öbür dildeki adı da takma ad olarak
/// aranır (İngilizce arayüzde "ses", Türkçe arayüzde "sound" yazan da bulur). Son öğe her zaman genel "Ayarlar"dır.
/// </summary>
public static class SettingsPages
{
    private static readonly (string Key, string Target)[] Pages =
    [
        (nameof(CoreStrings.SettingsPage_Sound), "ms-settings:sound"),
        (nameof(CoreStrings.SettingsPage_Display), "ms-settings:display"),
        (nameof(CoreStrings.SettingsPage_Bluetooth), "ms-settings:bluetooth"),
        (nameof(CoreStrings.SettingsPage_Wifi), "ms-settings:network-wifi"),
        (nameof(CoreStrings.SettingsPage_Apps), "ms-settings:appsfeatures"),
        (nameof(CoreStrings.SettingsPage_Personalization), "ms-settings:personalization"),
        (nameof(CoreStrings.SettingsPage_Background), "ms-settings:personalization-background"),
        (nameof(CoreStrings.SettingsPage_Power), "ms-settings:powersleep"),
        (nameof(CoreStrings.SettingsPage_Update), "ms-settings:windowsupdate"),
        (nameof(CoreStrings.SettingsPage_Notifications), "ms-settings:notifications"),
        (nameof(CoreStrings.SettingsPage_Mouse), "ms-settings:mousetouchpad"),
        (nameof(CoreStrings.SettingsPage_Keyboard), "ms-settings:keyboard"),
        (nameof(CoreStrings.SettingsPage_Storage), "ms-settings:storagesense"),
        (nameof(CoreStrings.SettingsPage_Settings), "ms-settings:"),
    ];

    public static IReadOnlyList<SearchItem> For(CultureInfo ui)
    {
        var other = UiLanguage.IsTurkish(ui) ? UiLanguage.English : UiLanguage.Turkish;
        return Pages.Select(p => new SearchItem(CoreText.Get(p.Key, ui), SearchKind.Setting, p.Target, [CoreText.Get(p.Key, other)]))
            .ToList();
    }
}
