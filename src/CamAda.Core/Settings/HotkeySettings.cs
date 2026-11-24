namespace CamAda.Core.Settings;

/// <summary>Genel kısayollar (spec §8, §11). Geçersiz metin yüklenirken varsayılana döner.</summary>
public sealed record HotkeySettings
{
    public const string DefaultMicToggle = "Ctrl+Shift+M";

    /// <summary>Mikrofonu her yerden kapatıp açar (spec §7.4).</summary>
    public string MicToggle { get; init; } = DefaultMicToggle;
}
