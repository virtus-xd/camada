namespace CamAda.Core.Island;

/// <summary>Ada hapının boyutu (DIP). Köşe yarıçapı her zaman yüksekliğin yarısıdır.</summary>
public readonly record struct PillSize(double Width, double Height);

/// <summary>Spec'te sabitlenen ada ölçüleri.</summary>
public static class IslandLayout
{
    public static readonly PillSize Compact = new(400, 40);
    public static readonly PillSize Expanded = new(770, 46);

    /// <summary>Hapın ekranın üst kenarından uzaklığı.</summary>
    public const double TopMargin = 8;

    /// <summary>Ada penceresinin genişliği: en geniş hap (1100) + gölge payı.</summary>
    public const double WindowWidth = 1200;

    public const double WindowHeight = 160;

    /// <summary>Genişlemiş hapın üst sınırı.</summary>
    public const double MaxExpandedWidth = 1100;

    /// <summary>Akıllı gizlenmede adayı açan üst kenar bölgesinin genişliği (ortada).</summary>
    public const double RevealZoneWidth = 900;

    private const int ButtonsThatFitBase = 5;
    private const double ButtonPitch = 38;

    /// <summary>
    /// Genişlemiş hap genişliği: 5 uygulama düğmesine kadar 770 DIP; her ek düğme ("+N" dahil) 38 DIP, en fazla 1100.
    /// </summary>
    public static double ExpandedWidthFor(int appButtonCount) =>
        Math.Min(MaxExpandedWidth, Expanded.Width + Math.Max(0, appButtonCount - ButtonsThatFitBase) * ButtonPitch);
}
