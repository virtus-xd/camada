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

    /// <summary>Ana ekranın üstünde adaya ayrılan şeridin yüksekliği (DIP).</summary>
    public double StripHeight { get; init; } = 56;

    public int HoverEnterDelayMs { get; init; } = 120;

    public int HoverLeaveDelayMs { get; init; } = 350;
}
