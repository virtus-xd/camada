using System.Runtime.InteropServices;
using CamAda.Core.Network;
using Windows.Devices.WiFi;
using Windows.Networking.Connectivity;
using Windows.Security.Credentials;

namespace CamAda.QuickSettings;

/// <summary>Bir taramanın sonucu: satırlar ve ağ adlarının konum izni yüzünden gizlenip gizlenmediği.</summary>
public sealed record WifiScan(IReadOnlyList<WifiRow> Rows, bool LocationBlocked);

public enum WifiConnectOutcome
{
    Connected,
    WrongPassword,
    NotFound,
    Failed,
}

/// <summary>
/// Wi‑Fi listesi (belgelenmiş <c>Windows.Devices.WiFi</c>, Plan 4.3): tarama, kayıtlı ağa ya da şifreyle bağlanma,
/// bağlantıyı kesme. Şifre yalnız bağlanma çağrısına verilir, hiçbir yerde tutulmaz (Windows kendi ağ profiline yazar).
/// Çağrılar arayüz iş parçacığından yapılır; WinRT işlemleri beklenirken arayüz serbesttir.
/// </summary>
public sealed class WifiService
{
    private readonly Dictionary<string, WiFiAvailableNetwork> _networks = new(StringComparer.Ordinal);
    private WiFiAdapter? _adapter;

    private WifiService(WiFiAdapter? adapter)
    {
        _adapter = adapter;
    }

    /// <summary>
    /// Wi‑Fi adaptörü olmayan bilgisayarda null. Windows erişimi şimdilik vermiyorsa (konum izni) servis yine kurulur;
    /// adaptör her taramada yeniden istenir, izin sonradan açılınca liste kendiliğinden gelir.
    /// </summary>
    public static async Task<WifiService?> CreateAsync()
    {
        if (await WiFiAdapter.RequestAccessAsync() != WiFiAccessStatus.Allowed)
        {
            return new WifiService(null);
        }

        var adapters = await WiFiAdapter.FindAllAdaptersAsync();
        return adapters.Count == 0 ? null : new WifiService(adapters[0]);
    }

    /// <summary>
    /// Adaptörsüz kurar: erişim izni ilk taramada, yani Wi‑Fi listesi ilk açıldığında istenir. Paketli uygulamada izin
    /// "konum" penceresi açtığı için açılışta istenmez.
    /// </summary>
    public static WifiService CreateDeferred() => new(null);

    public async Task<WifiScan> ScanAsync(CancellationToken cancellation)
    {
        try
        {
            if (!await EnsureAdapterAsync())
            {
                return new WifiScan([], LocationBlocked: true);
            }

            return await ScanCoreAsync(_adapter!, cancellation);
        }
        catch (UnauthorizedAccessException)
        {
            _adapter = null; // Windows ağ bilgisini vermiyor (konum izni); bir dahaki taramada yeniden istenir
            return new WifiScan([], LocationBlocked: true);
        }
        catch (COMException)
        {
            _adapter = null; // adaptör yeniden numaralanmış olabilir (uyku, sürücü); bir dahaki taramada yeniden bulunur
            throw;
        }
    }

    private async Task<bool> EnsureAdapterAsync()
    {
        if (_adapter is not null)
        {
            return true;
        }

        if (await WiFiAdapter.RequestAccessAsync() != WiFiAccessStatus.Allowed)
        {
            return false;
        }

        _adapter = (await WiFiAdapter.FindAllAdaptersAsync()).FirstOrDefault();
        return _adapter is not null;
    }

    private async Task<WifiScan> ScanCoreAsync(WiFiAdapter adapter, CancellationToken cancellation)
    {
        await adapter.ScanAsync().AsTask(cancellation);
        var seen = new List<WifiNetworkSeen>();
        var total = 0;
        _networks.Clear();
        foreach (var network in adapter.NetworkReport.AvailableNetworks)
        {
            total++;
            if (string.IsNullOrWhiteSpace(network.Ssid))
            {
                continue;
            }

            var security = WifiNetworkList.SecurityFrom(
                network.SecuritySettings.NetworkAuthenticationType.ToString(),
                network.SecuritySettings.NetworkEncryptionType.ToString());
            seen.Add(new WifiNetworkSeen(network.Ssid, NetworkIcons.BarsFrom(network.SignalBars, 4), security));
            if (!_networks.TryGetValue(network.Ssid, out var known) || network.SignalBars > known.SignalBars)
            {
                _networks[network.Ssid] = network;
            }
        }

        var saved = NetworkInformation.GetConnectionProfiles()
            .Where(p => p.IsWlanConnectionProfile)
            .Select(p => p.ProfileName)
            .ToHashSet(StringComparer.Ordinal);

        WifiNetworkSeen? connected = null;
        var profile = await adapter.NetworkAdapter.GetConnectedProfileAsync().AsTask(cancellation);
        if (profile?.IsWlanConnectionProfile == true && profile.WlanConnectionProfileDetails.GetConnectedSsid() is { Length: > 0 } ssid)
        {
            var security = seen.FirstOrDefault(s => s.Ssid == ssid)?.Security ?? WifiSecurity.Password;
            connected = new WifiNetworkSeen(ssid, NetworkIcons.BarsFrom(profile.GetSignalBars() ?? 0, 5), security);
        }

        return new WifiScan(WifiNetworkList.Build(seen, connected, saved),
            WifiNetworkList.LooksLocationBlocked(total, seen.Count));
    }

    /// <summary>
    /// Son taramada görülen ağa bağlanır; şifre verilirse yeni profil "otomatik bağlan" olarak kaydedilir. Panel kapansa da
    /// bağlanma sürer (iptal edilmez).
    /// </summary>
    public async Task<WifiConnectOutcome> ConnectAsync(string ssid, string? password)
    {
        if (_adapter is null || !_networks.TryGetValue(ssid, out var network))
        {
            return WifiConnectOutcome.NotFound;
        }

        try
        {
            var result = password is null
                ? await _adapter.ConnectAsync(network, WiFiReconnectionKind.Automatic)
                : await _adapter.ConnectAsync(network, WiFiReconnectionKind.Automatic, new PasswordCredential { Password = password });
            return result.ConnectionStatus switch
            {
                WiFiConnectionStatus.Success => WifiConnectOutcome.Connected,
                WiFiConnectionStatus.InvalidCredential => WifiConnectOutcome.WrongPassword,
                WiFiConnectionStatus.NetworkNotAvailable => WifiConnectOutcome.NotFound,
                _ => WifiConnectOutcome.Failed,
            };
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException or ArgumentException)
        {
            return WifiConnectOutcome.Failed;
        }
    }

    public void Disconnect()
    {
        try
        {
            _adapter?.Disconnect();
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            _adapter = null;
        }
    }
}
