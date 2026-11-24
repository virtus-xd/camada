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
    /// <summary>Ayar dosyasının biçim sürümü; eski dosyalar yüklenirken bir kereye mahsus güncellenir.</summary>
    public const int CurrentVersion = 2;

    public int SettingsVersion { get; init; } = CurrentVersion;

    /// <summary>Windows görev çubuğu gizlensin mi (ada onun yerini alır).</summary>
    public bool HideWindowsTaskbar { get; init; } = true;

    /// <summary>Oturum açılınca otomatik başlasın mı (HKCU\...\Run).</summary>
    public bool StartWithWindows { get; init; } = true;

    public GlassMode GlassMode { get; init; } = GlassMode.Wallpaper;

    /// <summary>
    /// Tam ekranı algılayan görünmez şerit penceresinin yüksekliği (DIP). Akıllı gizlenmeden beri alan ayırmaz (spec §7.6).
    /// </summary>
    public double StripHeight { get; init; } = 56;

    public int HoverEnterDelayMs { get; init; } = 120;

    public int HoverLeaveDelayMs { get; init; } = 350;

    /// <summary>Adada sabitlenmiş uygulamalar. null: henüz belirlenmedi, ilk açılışta bilgisayarda aranır.</summary>
    public IReadOnlyList<Apps.PinnedApp>? PinnedApps { get; init; }

    /// <summary>Boştaki adada gösterilen hava durumunun konumu. null: hava durumu gösterilmez.</summary>
    public WeatherLocation? Weather { get; init; }

    /// <summary>Masaüstündeki plak çalar (spec §7.8).</summary>
    public RecordSettings Record { get; init; } = new();
}

/// <summary>
/// Hava durumu konumu: şehir adı ve (bilinmiyorsa null) koordinatlar. Koordinat yoksa uygulama adı bir kez Open-Meteo'da
/// arayıp bulduğu koordinatları ayara yazar; şehir değişince yalnız ad yazılıp koordinatlar silinir.
/// </summary>
public sealed record WeatherLocation(string Name, double? Latitude, double? Longitude);
