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

    /// <summary>Örtülü yerde kısa gösterge: ada görünür ama tıklamalar alttaki pencereye geçer.</summary>
    Peek,
}

/// <summary>Adanın gösterdiği kısa genişleme (spec §7.5).</summary>
public enum FlashKind
{
    None,

    /// <summary>Bilgi göstergesi (ör. ses değişti): örtülü yerde tıklamaları alttaki pencereye geçirir.</summary>
    Passive,

    /// <summary>Düğmeli önizleme (ekran görüntüsü: Kopyala / Düzenle): örtülü yerde de tıklanabilir (Plan 4.2).</summary>
    Interactive,
}

public static class IslandPresenceRule
{
    /// <param name="flash">
    /// Ada kısa bir gösterge gösteriyor: örtülü yerde de görünür. Bilgi göstergesi tıklamaları alttaki pencereye geçirir
    /// (Peek; spec §7.6 "çizgi tıklamaları engellemez", §4 sekme alanı); düğmeli önizleme tıklanabilir kalır.
    /// </param>
    public static IslandPresence Decide(bool fullScreen, bool occluded, bool expanded, FlashKind flash) =>
        fullScreen ? IslandPresence.Hidden
        : !occluded || expanded ? IslandPresence.Shown
        : flash switch
        {
            FlashKind.Interactive => IslandPresence.Shown,
            FlashKind.Passive => IslandPresence.Peek,
            _ => IslandPresence.Tucked,
        };
}
