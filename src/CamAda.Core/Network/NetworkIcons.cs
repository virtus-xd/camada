namespace CamAda.Core.Network;

/// <summary>İnternete çıkılan bağlantının türü.</summary>
public enum NetworkLink
{
    None,
    Ethernet,
    Wifi,
}

/// <summary>Bağlantının ulaştığı yer: hiç, yalnız yerel ağ (ya da giriş sayfası) ya da internet.</summary>
public enum NetworkReach
{
    None,
    Local,
    Internet,
}

/// <summary>Adadaki Wi‑Fi simgesinin girdisi (Plan 4.3). <paramref name="Bars"/> 1–4 arasıdır.</summary>
public sealed record NetworkSnapshot(
    NetworkLink Link, NetworkReach Reach, int Bars, bool HasWifiAdapter, bool WifiRadioOn, string? Ssid);

public enum NetworkIconKind
{
    Ethernet,
    Wifi,
    WifiDisconnected,
    WifiOff,
    NoNetwork,
}

/// <summary>Simge türü, Wi‑Fi çubukları (Ethernet'te 0) ve internet yok uyarısı.</summary>
public sealed record NetworkIcon(NetworkIconKind Kind, int Bars, bool Warning);

/// <summary>Adadaki ağ simgesinin, erişilebilirlik adının ve Wi‑Fi kutucuğu alt yazısının kuralları.</summary>
public static class NetworkIcons
{
    public static NetworkIcon For(NetworkSnapshot s) => s.Link switch
    {
        NetworkLink.Ethernet => new NetworkIcon(NetworkIconKind.Ethernet, 0, s.Reach != NetworkReach.Internet),
        NetworkLink.Wifi => new NetworkIcon(NetworkIconKind.Wifi, Math.Clamp(s.Bars, 1, 4), s.Reach != NetworkReach.Internet),
        _ when !s.HasWifiAdapter => new NetworkIcon(NetworkIconKind.NoNetwork, 0, false),
        _ when !s.WifiRadioOn => new NetworkIcon(NetworkIconKind.WifiOff, 0, false),
        _ => new NetworkIcon(NetworkIconKind.WifiDisconnected, 0, false),
    };

    public static string AccessibleName(NetworkSnapshot s)
    {
        var internet = s.Reach == NetworkReach.Internet ? "internet var" : "internet yok";
        return s.Link switch
        {
            NetworkLink.Ethernet => $"Ethernet, {internet}",
            NetworkLink.Wifi => $"Wi‑Fi: {s.Ssid}, {internet}",
            _ when !s.HasWifiAdapter => "Ağ bağlantısı yok",
            _ when !s.WifiRadioOn => "Wi‑Fi kapalı",
            _ => "Wi‑Fi: bağlı değil",
        };
    }

    public static string WifiTileSubtitle(NetworkSnapshot s) =>
        !s.HasWifiAdapter ? "Yok"
        : !s.WifiRadioOn ? "Kapalı"
        : s.Link == NetworkLink.Wifi && !string.IsNullOrEmpty(s.Ssid) ? s.Ssid
        : "Bağlı değil";

    /// <summary>Windows'un 0–<paramref name="max"/> çubuğunu adanın 1–4 çubuğuna çevirir (yukarı yuvarlar).</summary>
    public static int BarsFrom(int raw, int max) =>
        max <= 0 ? 1 : Math.Clamp((int)Math.Ceiling(raw * 4.0 / max), 1, 4);
}
