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

    /// <summary>Ada penceresinin genişliği; gölge ve ileride kısa genişlemeler için pay bırakır.</summary>
    public const double WindowWidth = 900;

    public const double WindowHeight = 160;

    /// <summary>Akıllı gizlenmede adayı açan üst kenar bölgesinin genişliği (ortada).</summary>
    public const double RevealZoneWidth = 900;
}
