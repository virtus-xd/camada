using System.Globalization;

namespace CamAda.Core.Network;

/// <summary>Panelden nasıl bağlanılır: şifresiz, şifreyle ya da (kurumsal) Windows Ayarları'nda.</summary>
public enum WifiSecurity
{
    Open,
    Password,
    Enterprise,
}

/// <summary>Taramada görülen bir erişim noktası; <paramref name="Bars"/> 1–4.</summary>
public sealed record WifiNetworkSeen(string Ssid, int Bars, WifiSecurity Security);

/// <summary>Wi‑Fi listesinin bir satırı.</summary>
public sealed record WifiRow(string Ssid, int Bars, WifiSecurity Security, bool Connected, bool Saved);

/// <summary>Wi‑Fi listesinin kurulması (tasarım "Wi‑Fi listesi").</summary>
public static class WifiNetworkList
{
    private static readonly StringComparer NameOrder = StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true);

    /// <summary>
    /// Adsız (gizli) ağları atar, aynı adı en güçlü sinyalle tek satır yapar, bağlı ağı (görülmese de) ekler. Sıra: bağlı,
    /// kayıtlı, diğer; her grupta sinyal azalan, sonra ad.
    /// </summary>
    public static IReadOnlyList<WifiRow> Build(
        IEnumerable<WifiNetworkSeen> seen, WifiNetworkSeen? connected, IReadOnlySet<string> savedSsids)
    {
        var strongest = new Dictionary<string, WifiNetworkSeen>(StringComparer.Ordinal);
        foreach (var network in connected is null ? seen : seen.Append(connected))
        {
            if (string.IsNullOrWhiteSpace(network.Ssid))
            {
                continue;
            }

            if (!strongest.TryGetValue(network.Ssid, out var known) || network.Bars > known.Bars)
            {
                strongest[network.Ssid] = network;
            }
        }

        return strongest.Values
            .Select(n => new WifiRow(n.Ssid, n.Bars, n.Security,
                Connected: connected is not null && n.Ssid == connected.Ssid,
                Saved: savedSsids.Contains(n.Ssid)))
            .OrderBy(r => r.Connected ? 0 : r.Saved ? 1 : 2)
            .ThenByDescending(r => r.Bars)
            .ThenBy(r => r.Ssid, NameOrder)
            .ToList();
    }

    /// <summary>
    /// WinRT <c>NetworkAuthenticationType</c> ve <c>NetworkEncryptionType</c> adlarından: şifresiz (şifrelemesiz açık ya da
    /// OWE), şifreli kişisel (WEP, WPA/WPA2-PSK, WPA3-SAE) ya da diğer her şey (kurumsal, bilinmeyen).
    /// </summary>
    public static WifiSecurity SecurityFrom(string authentication, string encryption) => authentication switch
    {
        "Open80211" or "None" => encryption.StartsWith("Wep", StringComparison.Ordinal) ? WifiSecurity.Password : WifiSecurity.Open,
        "Owe" => WifiSecurity.Open,
        "SharedKey80211" or "WpaPsk" or "RsnaPsk" or "Wpa3Sae" => WifiSecurity.Password,
        _ => WifiSecurity.Enterprise,
    };

    /// <summary>Ağlar görünüyor ama hiçbirinin adı yoksa Windows konum iznini vermiyordur.</summary>
    public static bool LooksLocationBlocked(int networkCount, int namedCount) => networkCount > 0 && namedCount == 0;
}
