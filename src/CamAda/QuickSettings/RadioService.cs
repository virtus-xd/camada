using System.Runtime.InteropServices;
using System.Windows.Threading;
using Windows.Devices.Radios;

namespace CamAda.QuickSettings;

/// <summary>
/// Wi‑Fi ve Bluetooth telsizleri (belgelenmiş <c>Windows.Devices.Radios</c>, Plan 4.3): durum, aç/kapat ve değişiklik
/// olayı. Windows telsiz erişimine izin vermezse ya da adaptör yoksa ilgili telsiz yok sayılır.
/// </summary>
public sealed class RadioService : IDisposable
{
    private readonly Dispatcher _dispatcher;
    private readonly Radio? _wifi;
    private readonly Radio? _bluetooth;
    private bool _disposed;

    private RadioService(Dispatcher dispatcher, Radio? wifi, Radio? bluetooth)
    {
        _dispatcher = dispatcher;
        _wifi = wifi;
        _bluetooth = bluetooth;
        foreach (var radio in Radios)
        {
            radio.StateChanged += OnStateChanged;
        }
    }

    public static async Task<RadioService> CreateAsync(Dispatcher dispatcher)
    {
        if (await Radio.RequestAccessAsync() != RadioAccessStatus.Allowed)
        {
            return new RadioService(dispatcher, null, null);
        }

        var radios = await Radio.GetRadiosAsync();
        return new RadioService(dispatcher,
            radios.FirstOrDefault(r => r.Kind == RadioKind.WiFi),
            radios.FirstOrDefault(r => r.Kind == RadioKind.Bluetooth));
    }

    public bool HasWifi => _wifi is not null;

    public bool HasBluetooth => _bluetooth is not null;

    public bool WifiOn => _wifi?.State == RadioState.On;

    public bool BluetoothOn => _bluetooth?.State == RadioState.On;

    /// <summary>Bir telsizin durumu değişince (arayüz iş parçacığında).</summary>
    public event EventHandler? Changed;

    /// <summary>Telsizi açar ya da kapatır; telsiz yoksa ya da Windows reddederse false.</summary>
    public async Task<bool> SetAsync(RadioKind kind, bool on)
    {
        var radio = kind switch
        {
            RadioKind.WiFi => _wifi,
            RadioKind.Bluetooth => _bluetooth,
            _ => null,
        };
        if (radio is null)
        {
            return false;
        }

        try
        {
            return await radio.SetStateAsync(on ? RadioState.On : RadioState.Off) == RadioAccessStatus.Allowed;
        }
        catch (Exception ex) when (ex is COMException or UnauthorizedAccessException)
        {
            return false;
        }
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var radio in Radios)
        {
            radio.StateChanged -= OnStateChanged;
        }
    }

    private IEnumerable<Radio> Radios => new[] { _wifi, _bluetooth }.OfType<Radio>();

    private void OnStateChanged(Radio sender, object args) => _dispatcher.BeginInvoke(() =>
    {
        if (!_disposed)
        {
            Changed?.Invoke(this, EventArgs.Empty);
        }
    });
}
