using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using CamAda.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.CoreAudioApi.Interfaces;

namespace CamAda.Audio;

/// <summary>Varsayılan çıkışın ses düzeyi değişti.</summary>
/// <param name="FromCamAda">Değişikliği CamAda'nın kendisi yaptıysa (ör. Ses panelindeki kaydırıcı).</param>
public sealed record MasterVolumeChange(float Volume, bool Muted, bool FromCamAda);

/// <summary>
/// Windows Core Audio (spec §8 "Ses"): varsayılan çıkışın ses düzeyi, çıkış cihazları ve varsayılanı değiştirme. Olay
/// tabanlıdır (yoklama yok). Core Audio geri çağrıları başka iş parçacıklarından gelir; burada hiçbir Core Audio çağrısı
/// yapılmaz, her şey UI iş parçacığına aktarılır ve olaylar orada yayımlanır. Cihaz yokken ya da cihaz kaybolmuşken üyeler
/// 0/false döner, ayarlayıcılar bir şey yapmaz.
/// </summary>
public sealed class AudioService : IDisposable
{
    /// <summary>CamAda'nın yaptığı değişikliklerin olay bağlamı: bildirimde bu Guid görülürse değişiklik bizimdir.</summary>
    private static readonly Guid OwnContext = new("6f0b2a4e-5d1c-4b8e-9a3f-2c7d8e1f4a60");

    private readonly Dispatcher _dispatcher;
    private readonly MMDeviceEnumerator _enumerator = new();
    private readonly DeviceNotifications _notifications;
    private MMDevice? _output;
    private AudioEndpointVolume? _outputVolume;
    private bool _disposed;

    public AudioService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _notifications = new DeviceNotifications(this);
        _enumerator.RegisterEndpointNotificationCallback(_notifications);
        AttachOutput();
    }

    /// <summary>Varsayılan çıkışın ses düzeyi değişti (UI iş parçacığında).</summary>
    public event EventHandler<MasterVolumeChange>? MasterChanged;

    /// <summary>Varsayılan cihaz değişti ya da cihaz eklendi, kaldırıldı, durumu değişti (UI iş parçacığında).</summary>
    public event EventHandler? DevicesChanged;

    public bool HasOutput => _outputVolume is not null;

    public float Volume => Read(() => _outputVolume?.MasterVolumeLevelScalar ?? 0f, 0f);

    public bool Muted => Read(() => _outputVolume?.Mute ?? false, false);

    public void SetVolume(float volume) => Write(() =>
    {
        if (_outputVolume is { } endpoint)
        {
            endpoint.MasterVolumeLevelScalar = Math.Clamp(volume, 0f, 1f);
        }
    });

    public void SetMuted(bool muted) => Write(() =>
    {
        if (_outputVolume is { } endpoint)
        {
            endpoint.Mute = muted;
        }
    });

    /// <summary>Etkin çıkış cihazları; varsayılan işaretli.</summary>
    public IReadOnlyList<AudioOutput> Outputs() => Read<IReadOnlyList<AudioOutput>>(() =>
    {
        var defaultId = _output?.ID;
        var outputs = new List<AudioOutput>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active))
        {
            using (device)
            {
                outputs.Add(new AudioOutput(device.ID, device.FriendlyName, device.ID == defaultId));
            }
        }

        return outputs;
    }, []);

    /// <summary>Cihazı üç rolün varsayılanı yapar; Windows'un bildirimi gelince <see cref="DevicesChanged"/> yayımlanır.</summary>
    public void SetDefaultOutput(string deviceId) => Write(() => PolicyConfig.SetDefaultEndpoint(deviceId));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Write(() => _enumerator.UnregisterEndpointNotificationCallback(_notifications));
        DetachOutput();
        _enumerator.Dispose();
    }

    /// <summary>Varsayılan çıkışı (multimedya rolü) bağlar; önceki cihazın aboneliği bırakılır.</summary>
    private void AttachOutput()
    {
        DetachOutput();
        Write(() =>
        {
            if (!_enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
            {
                return;
            }

            _output = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _outputVolume = _output.AudioEndpointVolume;
            _outputVolume.NotificationGuid = OwnContext; // CamAda'nın yazdıkları bu bağlamla bildirilir
            _outputVolume.OnVolumeNotification += OnOutputVolume;
        });

        if (_outputVolume is null)
        {
            DetachOutput();
        }
    }

    private void DetachOutput()
    {
        if (_outputVolume is { } endpoint)
        {
            endpoint.OnVolumeNotification -= OnOutputVolume;
        }

        _outputVolume = null;
        var output = _output;
        _output = null;
        Write(() => output?.Dispose()); // AudioEndpointVolume'u ve geri çağrı kaydını da bırakır
    }

    /// <summary>Core Audio iş parçacığından gelir.</summary>
    private void OnOutputVolume(AudioVolumeNotificationData data)
    {
        var change = new MasterVolumeChange(data.MasterVolume, data.Muted, data.EventContext == OwnContext);
        _dispatcher.BeginInvoke(() =>
        {
            if (!_disposed)
            {
                MasterChanged?.Invoke(this, change);
            }
        });
    }

    /// <summary>Cihaz bildirimi (başka iş parçacığından): UI'da yeniden bağlanır ve olay yayımlanır.</summary>
    private void OnDevicesChanged(bool defaultOutputChanged) => _dispatcher.BeginInvoke(() =>
    {
        if (_disposed)
        {
            return;
        }

        if (defaultOutputChanged || _outputVolume is null)
        {
            AttachOutput();
            MasterChanged?.Invoke(this, new MasterVolumeChange(Volume, Muted, FromCamAda: true));
        }

        DevicesChanged?.Invoke(this, EventArgs.Empty);
    });

    private static T Read<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"Core Audio okunamadı: {ex.Message}");
            return fallback;
        }
    }

    private static void Write(Action write)
    {
        try
        {
            write();
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"Core Audio yazılamadı: {ex.Message}");
        }
    }

    /// <summary>IMMNotificationClient: yalnız ne olduğunu not eder, Core Audio'ya dokunmaz.</summary>
    private sealed class DeviceNotifications(AudioService owner) : IMMNotificationClient
    {
        public void OnDeviceStateChanged(string deviceId, DeviceState newState) => owner.OnDevicesChanged(false);

        public void OnDeviceAdded(string pwstrDeviceId) => owner.OnDevicesChanged(false);

        public void OnDeviceRemoved(string deviceId) => owner.OnDevicesChanged(false);

        public void OnDefaultDeviceChanged(DataFlow flow, Role role, string defaultDeviceId) =>
            owner.OnDevicesChanged(flow == DataFlow.Render && role == Role.Multimedia);

        public void OnPropertyValueChanged(string pwstrDeviceId, PropertyKey key)
        {
        }
    }
}
