namespace CamAda.Core.Island;

/// <summary>Adanın ekrandaki hâli.</summary>
public enum IslandPresence
{
    /// <summary>Tam ekran uygulama: ada ve çizgi tamamen gizli.</summary>
    Hidden,

    /// <summary>Adanın yerini bir pencere kaplıyor: ada ince bir çizgiye inmiş.</summary>
    Tucked,

    /// <summary>Ada görünür (küçük ya da genişlemiş).</summary>
    Shown,
}

public static class IslandPresenceRule
{
    /// <param name="flashing">Ada kısa bir gösterge gösteriyor (ör. ses değişti, spec §7.5): çizgiye inmişse de görünür.</param>
    public static IslandPresence Decide(bool fullScreen, bool occluded, bool expanded, bool flashing) =>
        fullScreen ? IslandPresence.Hidden
        : occluded && !expanded && !flashing ? IslandPresence.Tucked
        : IslandPresence.Shown;
}
