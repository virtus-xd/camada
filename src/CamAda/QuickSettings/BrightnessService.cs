using System.Management;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CamAda.Core.Display;
using CamAda.Interop;

namespace CamAda.QuickSettings;

/// <summary>Panelde bir parlaklık satırı.</summary>
public sealed record BrightnessDisplay(string Id, string Name, int Level, bool IsInternal);

/// <summary>Dahili ekranın parlaklığı değişti; <paramref name="FromCamAda"/> kendi kaydırıcımızın yankısı.</summary>
public sealed record BrightnessChange(int Level, bool FromCamAda);

/// <summary>
/// Parlaklık (Plan 4.3): dahili ekran Windows'un WMI parlaklık sınıflarıyla, harici ekranlar DDC/CI destekliyorsa
/// dxva2 ile. Sürüklerken gönderim seyrekleştirilir (dahili 100 ms, harici 250 ms; son değer hep gider). DDC çağrıları
/// yavaş olabildiği için tek sıralı işçide, arayüz iş parçacığı dışında yapılır. Dahili parlaklık değişikliği (Fn
/// tuşları) WMI olayıyla gelir; kendi gönderdiğimiz değerin yankısı <see cref="EchoFilter"/> ile ayırt edilir.
/// </summary>
public sealed class BrightnessService : IDisposable
{
    public const string InternalId = "internal";

    private static readonly TimeSpan InternalInterval = TimeSpan.FromMilliseconds(100);
    private static readonly TimeSpan ExternalInterval = TimeSpan.FromMilliseconds(250);

    private readonly Dispatcher _dispatcher;
    private readonly TimeProvider _time;
    private readonly EchoFilter _echo;
    private readonly SemaphoreSlim _ddc = new(1, 1);
    private readonly Dictionary<string, SendThrottle> _throttles = new();
    private readonly Dictionary<string, DispatcherTimer> _trailing = new();
    private readonly Dictionary<string, (nint Handle, uint Min, uint Max)> _external = new();
    private readonly List<MonitorApi.PHYSICAL_MONITOR[]> _ownedHandles = [];
    private readonly LatestValuePump _internalWriter;
    private ManagementObject? _internalMethods; // yalnız _internalWriter'ın sıralı yazımlarında kullanılır
    private ManagementEventWatcher? _watcher;
    private bool _disposed;

    public BrightnessService(Dispatcher dispatcher, TimeProvider time)
    {
        _dispatcher = dispatcher;
        _time = time;
        _echo = new EchoFilter(time, TimeSpan.FromSeconds(1.5));
        _internalWriter = new LatestValuePump(level => Task.Run(() => WriteInternal(level)));

        // WMI aboneliği açılışta yavaş olabilir (ya da WMI takılı olabilir): arayüz iş parçacığını bekletmesin.
        var watcher = new ManagementEventWatcher(new ManagementScope(@"root\WMI"),
            new EventQuery("SELECT * FROM WmiMonitorBrightnessEvent"));
        watcher.EventArrived += OnBrightnessEvent;
        _watcher = watcher;
        _ = Task.Run(() =>
        {
            try
            {
                if (!_disposed)
                {
                    watcher.Start();
                }
            }
            catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
            {
                // Dahili parlaklık olayı yok: adada gösterge çıkmaz, panel yine çalışır.
            }
        });
    }

    /// <summary>Son <see cref="RefreshAsync"/> ile okunan ekranlar (dahili önce).</summary>
    public IReadOnlyList<BrightnessDisplay> Displays { get; private set; } = [];

    /// <summary>Dahili ekranın parlaklığı değişti (arayüz iş parçacığında).</summary>
    public event EventHandler<BrightnessChange>? InternalChanged;

    /// <summary>Ekranları ve parlaklıklarını yeniden okur (panel açılınca).</summary>
    public async Task RefreshAsync()
    {
        var internalDisplay = await Task.Run(ReadInternal);
        var external = await ReadExternalAsync(internalDisplay?.Instance);
        if (_disposed)
        {
            return;
        }

        var list = new List<BrightnessDisplay>();
        if (internalDisplay is { } i)
        {
            list.Add(new BrightnessDisplay(InternalId, "Ekran", i.Level, true));
        }

        list.AddRange(external);
        Displays = list;
    }

    /// <summary>Kaydırıcıdan gelen değer: seyrekleştirilip ekrana gönderilir.</summary>
    public void Set(string id, int level)
    {
        level = Math.Clamp(level, 0, 100);
        if (!_throttles.TryGetValue(id, out var throttle))
        {
            throttle = new SendThrottle(_time, id == InternalId ? InternalInterval : ExternalInterval);
            _throttles[id] = throttle;
        }

        if (throttle.Offer(level) is { } now)
        {
            Send(id, now);
            return;
        }

        if (!_trailing.TryGetValue(id, out var timer))
        {
            timer = new DispatcherTimer(throttle.Interval, DispatcherPriority.Normal, (_, _) => SendDue(id), _dispatcher);
            _trailing[id] = timer;
        }

        timer.Start();
    }

    public void Dispose()
    {
        _disposed = true;
        foreach (var timer in _trailing.Values)
        {
            timer.Stop();
        }

        if (_watcher is not null)
        {
            _watcher.EventArrived -= OnBrightnessEvent;
            try
            {
                _watcher.Stop();
            }
            catch (Exception ex) when (ex is ManagementException or COMException)
            {
            }

            _watcher.Dispose();
        }

        _ = Task.Run(async () =>
        {
            await _ddc.WaitAsync();
            ReleaseExternalHandles();
        });
    }

    private void SendDue(string id)
    {
        var throttle = _throttles[id];
        if (throttle.TakeDue() is { } level)
        {
            Send(id, level);
        }

        if (!throttle.HasPending)
        {
            _trailing[id].Stop();
        }
    }

    private void Send(string id, int level)
    {
        if (id == InternalId)
        {
            lock (_echo)
            {
                _echo.Sent(level);
            }

            _internalWriter.Post(level); // tek yazıcı: eski bir yazım hiçbir zaman sona kalmaz
            return;
        }

        // _external yalnız DDC işçisinde (kilit altında) okunur ve yazılır.
        _ = Task.Run(async () =>
        {
            await _ddc.WaitAsync();
            try
            {
                if (!_disposed && _external.TryGetValue(id, out var monitor))
                {
                    var raw = (uint)Math.Round(monitor.Min + (monitor.Max - monitor.Min) * level / 100.0);
                    MonitorApi.SetMonitorBrightness(monitor.Handle, raw);
                }
            }
            finally
            {
                _ddc.Release();
            }
        });
    }

    private void OnBrightnessEvent(object sender, EventArrivedEventArgs e)
    {
        int level;
        try
        {
            level = Convert.ToInt32(e.NewEvent["Brightness"]);
        }
        catch (Exception ex) when (ex is ManagementException or InvalidCastException or FormatException)
        {
            return;
        }

        bool echo;
        lock (_echo)
        {
            echo = _echo.IsEcho(level);
        }

        _dispatcher.BeginInvoke(() =>
        {
            if (_disposed)
            {
                return;
            }

            Displays = Displays.Select(d => d.IsInternal ? d with { Level = level } : d).ToList();
            InternalChanged?.Invoke(this, new BrightnessChange(level, echo));
        });
    }

    private static (string Instance, int Level)? ReadInternal()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI",
                "SELECT InstanceName, CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE");
            foreach (ManagementObject item in searcher.Get())
            {
                using (item)
                {
                    return ((string)item["InstanceName"], Convert.ToInt32(item["CurrentBrightness"]));
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
        }

        return null;
    }

    /// <summary>Yalnız <see cref="_internalWriter"/> çağırır (sırayla, hiç üst üste binmeden).</summary>
    private void WriteInternal(int level)
    {
        try
        {
            _internalMethods ??= FindActiveMethods();
            _internalMethods?.InvokeMethod("WmiSetBrightness", [(uint)1, (byte)level]);
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
            _internalMethods?.Dispose();
            _internalMethods = null; // bir dahaki yazımda yeniden bulunur
        }
    }

    private static ManagementObject? FindActiveMethods()
    {
        using var methods = new ManagementClass(@"root\WMI", "WmiMonitorBrightnessMethods", null);
        foreach (ManagementObject instance in methods.GetInstances())
        {
            if (instance["Active"] is true)
            {
                return instance;
            }

            instance.Dispose();
        }

        return null;
    }

    /// <summary>DDC/CI'ya yanıt veren harici ekranlar; dahili ekranın WMI örneğiyle eşleşen atlanır.</summary>
    private async Task<IReadOnlyList<BrightnessDisplay>> ReadExternalAsync(string? internalInstance)
    {
        await _ddc.WaitAsync();
        try
        {
            return await Task.Run(() =>
            {
                ReleaseExternalHandles();
                var names = FriendlyNames();
                var result = new List<BrightnessDisplay>();
                foreach (var (hMonitor, device) in MonitorApi.Monitors())
                {
                    var instance = MonitorApi.InstanceIdOf(device);
                    if (instance is not null && internalInstance is not null
                        && internalInstance.StartsWith(instance, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (!MonitorApi.GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, out var count) || count == 0)
                    {
                        continue;
                    }

                    var physical = new MonitorApi.PHYSICAL_MONITOR[count];
                    if (!MonitorApi.GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physical))
                    {
                        continue;
                    }

                    _ownedHandles.Add(physical);
                    for (var i = 0; i < physical.Length; i++)
                    {
                        if (!MonitorApi.GetMonitorBrightness(physical[i].hPhysicalMonitor, out var min, out var cur, out var max)
                            || max <= min)
                        {
                            continue;
                        }

                        var id = $"ddc:{device}:{i}";
                        _external[id] = (physical[i].hPhysicalMonitor, min, max);
                        var name = instance is not null && names.TryGetValue(instance, out var friendly) ? friendly : "Harici ekran";
                        result.Add(new BrightnessDisplay(id, name, (int)Math.Round((cur - min) * 100.0 / (max - min)), false));
                    }
                }

                return (IReadOnlyList<BrightnessDisplay>)result;
            });
        }
        finally
        {
            _ddc.Release();
        }
    }

    /// <summary>EDID adları: WMI örnek adı (son "_0" olmadan) → "X24F165S".</summary>
    private static Dictionary<string, string> FriendlyNames()
    {
        var names = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\WMI", "SELECT InstanceName, UserFriendlyName FROM WmiMonitorID");
            foreach (ManagementObject item in searcher.Get())
            {
                using (item)
                {
                    var instance = (string)item["InstanceName"];
                    var underscore = instance.LastIndexOf('_');
                    var key = underscore > 0 ? instance[..underscore] : instance;
                    if (item["UserFriendlyName"] is ushort[] chars)
                    {
                        var name = new string(chars.TakeWhile(c => c != 0).Select(c => (char)c).ToArray()).Trim();
                        if (name.Length > 0)
                        {
                            names[key] = name;
                        }
                    }
                }
            }
        }
        catch (Exception ex) when (ex is ManagementException or COMException or UnauthorizedAccessException)
        {
        }

        return names;
    }

    /// <summary>Yalnız <see cref="_ddc"/> tutulurken çağrılır.</summary>
    private void ReleaseExternalHandles()
    {
        foreach (var monitors in _ownedHandles)
        {
            MonitorApi.DestroyPhysicalMonitors((uint)monitors.Length, monitors);
        }

        _ownedHandles.Clear();
        _external.Clear();
    }
}
