namespace CamAda.Core.Search;

/// <summary>Başlat panelinden aranabilen Windows Ayarlar sayfaları (Türkçe adlarıyla).</summary>
public static class SettingsPages
{
    public static IReadOnlyList<SearchItem> All { get; } =
    [
        Page("Ses ayarları", "ms-settings:sound"),
        Page("Ekran ayarları", "ms-settings:display"),
        Page("Bluetooth ve cihazlar", "ms-settings:bluetooth"),
        Page("Wi-Fi", "ms-settings:network-wifi"),
        Page("Uygulamalar", "ms-settings:appsfeatures"),
        Page("Kişiselleştirme", "ms-settings:personalization"),
        Page("Arka plan", "ms-settings:personalization-background"),
        Page("Güç ve pil", "ms-settings:powersleep"),
        Page("Windows Update", "ms-settings:windowsupdate"),
        Page("Bildirimler", "ms-settings:notifications"),
        Page("Fare", "ms-settings:mousetouchpad"),
        Page("Klavye", "ms-settings:keyboard"),
        Page("Depolama", "ms-settings:storagesense"),
        Page("Ayarlar", "ms-settings:"),
    ];

    private static SearchItem Page(string name, string target) => new(name, SearchKind.Setting, target);
}
