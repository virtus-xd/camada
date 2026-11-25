namespace CamAda.Core.Island;

/// <summary>Ada hapının boyutu (DIP). Köşe yarıçapı her zaman yüksekliğin yarısıdır.</summary>
public readonly record struct PillSize(double Width, double Height);

/// <summary>Spec'te sabitlenen ada ölçüleri.</summary>
public static class IslandLayout
{
    public static readonly PillSize Compact = new(400, 40);
    public static readonly PillSize Expanded = new(770, 46);

    /// <summary>Ekran görüntüsü önizlemesi (Plan 4.2): küçük resim + "Ekran görüntüsü" + Kopyala + Düzenle.</summary>
    public static readonly PillSize ScreenshotPeek = new(450, 76);

    /// <summary>Ada'nın oturum satırı (Plan 5): nokta ya da glif + tek satır metin.</summary>
    public static readonly PillSize AssistantPeek = new(560, 46);

    /// <summary>Hapın ekranın üst kenarından uzaklığı.</summary>
    public const double TopMargin = 8;

    /// <summary>Ada penceresinin genişliği: en geniş hap (1250) + gölge payı.</summary>
    public const double WindowWidth = 1350;

    public const double WindowHeight = 160;

    /// <summary>Genişlemiş hapın üst sınırı.</summary>
    public const double MaxExpandedWidth = 1250;

    /// <summary>Genişlemiş satırda uygulamalar ile ^ arasındaki müzik bölümü (mini plak + şarkı adı).</summary>
    public const double MusicSectionWidth = 150;

    /// <summary>
    /// Genişlemiş satırda ^ ile saat arasındaki sistem düğmeleri: pano (Plan 4.2), Wi‑Fi (Plan 4.3), ses (Plan 4.1) ve pil
    /// (Plan 4.3), 36'şar DIP. (Bildirim düğmesi Plan 4.4'le birlikte iptal edildi.)
    /// </summary>
    public const double SystemSectionWidth = 180;

    /// <summary>Akıllı gizlenmede adayı açan üst kenar bölgesinin genişliği (ortada).</summary>
    public const double RevealZoneWidth = 900;

    private const int ButtonsThatFitBase = 5;
    private const double ButtonPitch = 38;

    /// <summary>
    /// Genişlemiş hap genişliği: 5 uygulama düğmesine kadar 770 DIP + müzik bölümü + sistem düğmeleri (1064); her ek
    /// düğme ("+N" dahil) 38 DIP, en fazla 1250.
    /// </summary>
    public static double ExpandedWidthFor(int appButtonCount) =>
        Math.Min(MaxExpandedWidth,
            Expanded.Width + MusicSectionWidth + SystemSectionWidth
            + Math.Max(0, appButtonCount - ButtonsThatFitBase) * ButtonPitch);
}
