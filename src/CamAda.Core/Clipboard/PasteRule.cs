namespace CamAda.Core.Clipboard;

/// <summary>Kısayol anında öndeki pencere: hâlâ var mı, masaüstü ya da CamAda mı, yönetici olarak mı çalışıyor.</summary>
public sealed record PasteTarget(bool Exists, bool IsShellOrSelf, bool Elevated);

/// <summary>Seçimden sonra ne yapılacağı.</summary>
public enum PasteOutcome
{
    /// <summary>Yalnız panoya al (düğmeyle açılan panel ya da yapıştırılacak yer yok).</summary>
    CopyOnly,

    /// <summary>Panoya al ve önceki pencereye Ctrl+V gönder.</summary>
    Paste,

    /// <summary>Yapıştırılamıyor: panoya al, adada "Ctrl+V ile yapıştır" ipucu göster.</summary>
    CopyWithHint,
}

/// <summary>
/// Pano panelinde seçilen öğenin yapıştırılıp yapıştırılmayacağı (tasarım "Win+Alt+V ve yapıştırma"): yalnız kısayolla
/// açılmış panelde, hedef pencere duruyorsa ve Windows'un tuş göndermeye izin verdiği (yönetici olmayan) bir pencereyse.
/// </summary>
public static class PasteRule
{
    public static PasteOutcome Decide(bool openedByHotkey, PasteTarget? target) =>
        !openedByHotkey || target is null || target.IsShellOrSelf ? PasteOutcome.CopyOnly
        : !target.Exists || target.Elevated ? PasteOutcome.CopyWithHint
        : PasteOutcome.Paste;
}
