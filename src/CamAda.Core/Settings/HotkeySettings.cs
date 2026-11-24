namespace CamAda.Core.Settings;

/// <summary>Genel kısayollar (spec §8, §11). Geçersiz metin yüklenirken varsayılana döner.</summary>
public sealed record HotkeySettings
{
    public const string DefaultMicToggle = "Ctrl+Shift+M";

    public const string DefaultClipboardOpen = "Win+Alt+V";

    /// <summary>Mikrofonu her yerden kapatıp açar (spec §7.4).</summary>
    public string MicToggle { get; init; } = DefaultMicToggle;

    /// <summary>Pano panelini her yerden açar; seçilen öğe önceki pencereye yapıştırılır (Plan 4.2).</summary>
    public string ClipboardOpen { get; init; } = DefaultClipboardOpen;
}
