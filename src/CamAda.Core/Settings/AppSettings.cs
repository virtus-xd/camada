namespace CamAda.Core.Settings;

/// <summary>Ada ve plak çalarda kullanılan cam görünümü yöntemi.</summary>
public enum GlassMode
{
    /// <summary>Bulanıklaştırılmış duvar kağıdı + ton.</summary>
    Wallpaper,

    /// <summary>Windows akrilik efekti, pencere bölgesiyle şekillendirilmiş.</summary>
    Acrylic,

    /// <summary>Bulanıklıksız, daha opak düz ton.</summary>
    Solid,
}

/// <summary>%APPDATA%\CamAda\settings.json içinde saklanan kullanıcı ayarları.</summary>
public sealed record AppSettings
{
    /// <summary>Windows görev çubuğu gizlensin mi. Plan 2 bitene kadar varsayılan kapalı.</summary>
    public bool HideWindowsTaskbar { get; init; } = false;

    public GlassMode GlassMode { get; init; } = GlassMode.Wallpaper;

    /// <summary>
    /// Tam ekranı algılayan görünmez şerit penceresinin yüksekliği (DIP). Akıllı gizlenmeden beri alan ayırmaz (spec §7.6).
    /// </summary>
    public double StripHeight { get; init; } = 56;

    public int HoverEnterDelayMs { get; init; } = 120;

    public int HoverLeaveDelayMs { get; init; } = 350;

    /// <summary>Adada sabitlenmiş uygulamalar. null: henüz belirlenmedi, ilk açılışta bilgisayarda aranır.</summary>
    public IReadOnlyList<Apps.PinnedApp>? PinnedApps { get; init; }
}
