namespace CamAda.Core.Network;

/// <summary>Wi‑Fi listesi için Windows'tan erişim izninin ne zaman isteneceği.</summary>
public enum WifiStartupMode
{
    /// <summary>Wi‑Fi listesi yok (adaptör yok): izin hiç istenmez.</summary>
    None,

    /// <summary>Açılışta iste (paketsiz geliştirme kurulumu; izin penceresi çıkmaz).</summary>
    RequestNow,

    /// <summary>Wi‑Fi listesi ilk açıldığında iste (paketliyken izin "konum" penceresi açar).</summary>
    Deferred,
}

public static class WifiStartup
{
    public static WifiStartupMode Decide(bool packaged, bool hasWifiRadio) =>
        !packaged ? WifiStartupMode.RequestNow
        : hasWifiRadio ? WifiStartupMode.Deferred
        : WifiStartupMode.None;
}
