using System.Runtime.InteropServices;
using System.Windows.Threading;
using CamAda.Core.Network;
using Windows.Networking.Connectivity;

namespace CamAda.QuickSettings;

/// <summary>
/// Adadaki Wi‑Fi simgesinin durumu (Plan 4.3): internete çıkılan bağlantı, erişim düzeyi, Wi‑Fi adı ve sinyal gücü.
/// Olay tabanlı: ağ durumu ve telsiz değişince yeniden okunur; sinyal gücü için olay olmadığından App, ada genişlerken
/// <see cref="Refresh"/> çağırır (yoklama yok). Okuma (ağ hizmetine süreçler arası çağrılar) arayüz iş parçacığının
/// dışında yapılır; en son istenen okumanın sonucu geçerlidir.
/// </summary>
public sealed class NetworkStatusService : IDisposable
{
    private const uint EthernetType = 6;

    private readonly Dispatcher _dispatcher;
    private readonly RadioService? _radios;
    private bool _disposed;
    private int _version;

    public NetworkStatusService(Dispatcher dispatcher, RadioService? radios)
    {
        _dispatcher = dispatcher;
        _radios = radios;
        Current = new NetworkSnapshot(NetworkLink.None, NetworkReach.None, 0, radios?.HasWifi ?? false, radios?.WifiOn ?? false, null);
        try
        {
            NetworkInformation.NetworkStatusChanged += OnNetworkStatusChanged;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            // Ağ olayı yoksa durum yine ada genişlerken tazelenir.
        }

        if (radios is not null)
        {
            radios.Changed += OnRadiosChanged;
        }

        Refresh();
    }

    public NetworkSnapshot Current { get; private set; }

    /// <summary>Durum değişince (arayüz iş parçacığında).</summary>
    public event EventHandler? Changed;

    /// <summary>Durumu arka planda yeniden okur; sonuç (en son istenen) arayüz iş parçacığında uygulanır.</summary>
    public void Refresh()
    {
        if (_disposed)
        {
            return;
        }

        var version = ++_version;
        var hasWifi = _radios?.HasWifi ?? false;
        var wifiOn = _radios?.WifiOn ?? false;
        _ = Task.Run(() => Read(hasWifi, wifiOn)).ContinueWith(read => _dispatcher.BeginInvoke(() =>
        {
            if (_disposed || version != _version || read.IsFaulted || read.Result == Current)
            {
                return;
            }

            Current = read.Result;
            Changed?.Invoke(this, EventArgs.Empty);
        }), TaskScheduler.Default);
    }

    public void Dispose()
    {
        _disposed = true;
        NetworkInformation.NetworkStatusChanged -= OnNetworkStatusChanged;
        if (_radios is not null)
        {
            _radios.Changed -= OnRadiosChanged;
        }
    }

    private void OnNetworkStatusChanged(object sender) => _dispatcher.BeginInvoke(Refresh);

    private void OnRadiosChanged(object? sender, EventArgs e) => Refresh();

    private static NetworkSnapshot Read(bool hasWifi, bool wifiOn)
    {
        try
        {
            var internet = NetworkInformation.GetInternetConnectionProfile();
            var internetLevel = internet?.GetNetworkConnectivityLevel() ?? NetworkConnectivityLevel.None;

            // İnternet bir VPN üzerinden gidiyorsa ya da internet yoksa altta bağlı Wi‑Fi/Ethernet profilini göster.
            var link = internet is not null && internetLevel != NetworkConnectivityLevel.None && IsLink(internet)
                ? internet
                : NetworkInformation.GetConnectionProfiles().FirstOrDefault(p =>
                    IsLink(p) && p.GetNetworkConnectivityLevel() != NetworkConnectivityLevel.None);
            if (link is null)
            {
                return new NetworkSnapshot(NetworkLink.None, NetworkReach.None, 0, hasWifi, wifiOn, null);
            }

            var level = internetLevel == NetworkConnectivityLevel.InternetAccess
                ? NetworkConnectivityLevel.InternetAccess
                : link.GetNetworkConnectivityLevel();
            var reach = level switch
            {
                NetworkConnectivityLevel.InternetAccess => NetworkReach.Internet,
                NetworkConnectivityLevel.LocalAccess or NetworkConnectivityLevel.ConstrainedInternetAccess => NetworkReach.Local,
                _ => NetworkReach.None,
            };

            return link.IsWlanConnectionProfile
                ? new NetworkSnapshot(NetworkLink.Wifi, reach, NetworkIcons.BarsFrom(link.GetSignalBars() ?? 0, 5), hasWifi, wifiOn,
                    link.WlanConnectionProfileDetails.GetConnectedSsid())
                : new NetworkSnapshot(NetworkLink.Ethernet, reach, 0, hasWifi, wifiOn, null);
        }
        catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
        {
            return new NetworkSnapshot(NetworkLink.None, NetworkReach.None, 0, hasWifi, wifiOn, null);
        }
    }

    private static bool IsLink(ConnectionProfile profile) =>
        profile.IsWlanConnectionProfile || profile.NetworkAdapter?.IanaInterfaceType == EthernetType;
}
