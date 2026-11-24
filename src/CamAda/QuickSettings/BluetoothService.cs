using System.Runtime.InteropServices;
using System.Windows.Threading;
using CamAda.Core.Bluetooth;
using Windows.Devices.Bluetooth;
using Windows.Devices.Enumeration;

namespace CamAda.QuickSettings;

/// <summary>Eşleşmiş bir Bluetooth cihazı.</summary>
public sealed record BluetoothEntry(string Id, string Name, BluetoothDeviceKind Kind, bool Connected, string? ContainerId);

/// <summary>
/// Eşleşmiş Bluetooth cihazları (belgelenmiş <c>Windows.Devices.Enumeration</c>, Plan 4.3): yalnız panel açıkken iki
/// izleyici (klasik ve LE) çalışır; aynı cihaz iki izleyicide de görünürse (aynı kapsayıcı) klasik olan kalır. Ses
/// cihazları <see cref="BtAudioControl"/> ile bağlanıp ayrılır.
/// </summary>
public sealed class BluetoothService : IDisposable
{
    private const string IsConnectedKey = "System.Devices.Aep.IsConnected";
    private const string ContainerKey = "System.Devices.Aep.ContainerId";
    private const string MajorKey = "System.Devices.Aep.Bluetooth.Cod.Major";
    private const string MinorKey = "System.Devices.Aep.Bluetooth.Cod.Minor";
    private const string AppearanceKey = "System.Devices.Aep.Bluetooth.Le.Appearance";
    private static readonly string[] Properties = [IsConnectedKey, ContainerKey, MajorKey, MinorKey, AppearanceKey];
    private static readonly TimeSpan ConnectWait = TimeSpan.FromSeconds(10);

    private readonly Dispatcher _dispatcher;
    private readonly Dictionary<string, (DeviceInformation Info, bool Classic)> _devices = new();
    private readonly List<DeviceWatcher> _watchers = [];
    private readonly Dictionary<DeviceWatcher, bool> _classic = new();
    private int _users;

    public BluetoothService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>Spike'ta Bluetooth ses komutu zinciri doğrulandı (Görev 1).</summary>
    public bool CanSendAudioCommands => true;

    /// <summary>Bağlılar üstte, sonra ada göre.</summary>
    public IReadOnlyList<BluetoothEntry> Devices { get; private set; } = [];

    /// <summary>Liste değişince (arayüz iş parçacığında).</summary>
    public event EventHandler? Changed;

    /// <summary>İzleyicileri başlatır; her Start bir Stop ile eşleşir (panel ve liste sayfası ayrı ayrı çağırabilir).</summary>
    public void Start()
    {
        if (_users++ > 0)
        {
            return;
        }

        try
        {
            AddWatcher(BluetoothDevice.GetDeviceSelectorFromPairingState(true), classic: true);
            AddWatcher(BluetoothLEDevice.GetDeviceSelectorFromPairingState(true), classic: false);
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            // Bluetooth hizmeti yoksa liste boş kalır.
        }
    }

    public void Stop()
    {
        if (_users == 0 || --_users > 0)
        {
            return;
        }

        foreach (var watcher in _watchers)
        {
            watcher.Added -= OnAdded;
            watcher.Updated -= OnUpdated;
            watcher.Removed -= OnRemoved;
            if (watcher.Status is DeviceWatcherStatus.Started or DeviceWatcherStatus.EnumerationCompleted)
            {
                watcher.Stop();
            }
        }

        _watchers.Clear();
        _classic.Clear();
        _devices.Clear();
        Devices = [];
    }

    /// <summary>Ses cihazını bağlar; en çok 10 sn içinde bağlandıysa true.</summary>
    public Task<bool> ConnectAsync(BluetoothEntry device) => SendAsync(device, reconnect: true);

    public Task<bool> DisconnectAsync(BluetoothEntry device) => SendAsync(device, reconnect: false);

    public void Dispose()
    {
        _users = Math.Min(_users, 1);
        Stop();
    }

    private async Task<bool> SendAsync(BluetoothEntry device, bool reconnect)
    {
        if (device.ContainerId is not { } container || !await Task.Run(() => BtAudioControl.TrySend(container, reconnect)))
        {
            return false;
        }

        var deadline = DateTime.UtcNow + ConnectWait;
        while (DateTime.UtcNow < deadline)
        {
            if (Devices.FirstOrDefault(d => d.Id == device.Id)?.Connected == reconnect)
            {
                return true;
            }

            await Task.Delay(250);
        }

        return false;
    }

    private void AddWatcher(string selector, bool classic)
    {
        var watcher = DeviceInformation.CreateWatcher(selector, Properties, DeviceInformationKind.AssociationEndpoint);
        watcher.Added += OnAdded;
        watcher.Updated += OnUpdated;
        watcher.Removed += OnRemoved;
        _watchers.Add(watcher);
        _classic[watcher] = classic;
        watcher.Start();
    }

    private void OnAdded(DeviceWatcher sender, DeviceInformation info) => _dispatcher.BeginInvoke(() =>
    {
        if (!_classic.TryGetValue(sender, out var classic))
        {
            return; // durdurulmuş izleyiciden geç gelen olay
        }

        _devices[info.Id] = (info, classic);
        Publish();
    });

    private void OnUpdated(DeviceWatcher sender, DeviceInformationUpdate update) => _dispatcher.BeginInvoke(() =>
    {
        if (_classic.ContainsKey(sender) && _devices.TryGetValue(update.Id, out var known))
        {
            known.Info.Update(update);
            Publish();
        }
    });

    private void OnRemoved(DeviceWatcher sender, DeviceInformationUpdate update) => _dispatcher.BeginInvoke(() =>
    {
        if (_classic.ContainsKey(sender) && _devices.Remove(update.Id))
        {
            Publish();
        }
    });

    private void Publish()
    {
        var entries = _devices.Values
            .Select(d => (Entry: ToEntry(d.Info, d.Classic), d.Classic))
            .Where(d => !string.IsNullOrWhiteSpace(d.Entry.Name))
            .GroupBy(d => d.Entry.ContainerId ?? d.Entry.Id)
            .Select(g => g.OrderByDescending(d => d.Classic).First().Entry)
            .OrderByDescending(e => e.Connected)
            .ThenBy(e => e.Name, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
        Devices = entries;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static BluetoothEntry ToEntry(DeviceInformation info, bool classic)
    {
        var props = info.Properties;
        var connected = props.TryGetValue(IsConnectedKey, out var c) && c is true;
        var container = props.TryGetValue(ContainerKey, out var id) && id is Guid guid ? guid.ToString() : null;
        BluetoothDeviceKind kind;
        if (classic && props.TryGetValue(MajorKey, out var major) && major is not null)
        {
            var minor = props.TryGetValue(MinorKey, out var m) && m is not null ? Convert.ToUInt32(m) : 0u;
            kind = BluetoothKinds.FromMajorMinor(Convert.ToUInt32(major), minor);
        }
        else if (props.TryGetValue(AppearanceKey, out var appearance) && appearance is not null)
        {
            kind = BluetoothKinds.FromLeAppearance(Convert.ToUInt16(appearance));
        }
        else
        {
            kind = BluetoothDeviceKind.Other;
        }

        return new BluetoothEntry(info.Id, info.Name, kind, connected, container);
    }
}
