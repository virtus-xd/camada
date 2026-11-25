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
    private readonly ProcessInfoCache _processes = new(ProcessInfoReader.Read);
    private MMDevice? _output;
    private AudioEndpointVolume? _outputVolume;
    private AudioEndpointVolumeNotificationDelegate? _outputHandler;

    /// <summary>Her bağlanışta artar: eski cihazın kuyrukta kalan ses olayı yeni cihazın düzeyi gibi gösterilmez.</summary>
    private int _outputGeneration;
    private MMDevice? _micConsole;
    private MMDevice? _micCommunications;
    private bool _disposed;

    public AudioService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
        _notifications = new DeviceNotifications(this);
        _enumerator.RegisterEndpointNotificationCallback(_notifications);
        AttachOutput();
        AttachMic();
    }

    /// <summary>Varsayılan kayıt cihazının sessiz durumu ya da cihazın kendisi değişti (UI iş parçacığında).</summary>
    public event EventHandler? MicChanged;

    /// <summary>Konsol rolündeki varsayılan kayıt cihazının durumu.</summary>
    public MicState Mic => MicStates.From(_micConsole is not null, Read(() => _micConsole?.AudioEndpointVolume.Mute ?? false, false));

    public string? MicName => Read(() => _micConsole?.FriendlyName, null);

    /// <summary>
    /// Mikrofonu kapatır/açar: hedef, konsol varsayılanının şimdiki durumunun tersi; iletişim varsayılanı farklı bir
    /// cihazsa ona da uygulanır (Global Constraints). Kayıt cihazı yoksa bir şey yapmaz.
    /// </summary>
    public void ToggleMic()
    {
        if (_micConsole is not { } console)
        {
            return;
        }

        var target = !Read(() => console.AudioEndpointVolume.Mute, false);
        Write(() => console.AudioEndpointVolume.Mute = target);
        if (_micCommunications is { } communications)
        {
            Write(() => communications.AudioEndpointVolume.Mute = target);
        }
    }

    /// <summary>Varsayılan çıkışta o an ses çıkaran uygulamalar (Ses paneli açıkken saniyede bir okunur).</summary>
    public IReadOnlyList<AppVolumeRow> Apps() => Read<IReadOnlyList<AppVolumeRow>>(() =>
    {
        if (_output is null)
        {
            return [];
        }

        var samples = new List<SessionSample>();
        var pids = new HashSet<int>();
        ForEachSession(session =>
        {
            if (session.IsSystemSoundsSession)
            {
                return; // Sistem sesleri gösterilmez; süreci (Idle, 0) aranmaz
            }

            var pid = (int)session.GetProcessID;
            pids.Add(pid);
            if (_processes.Get(pid) is not { } process)
            {
                return; // süreç bu arada kapandı
            }

            var name = session.DisplayName;
            if (string.IsNullOrWhiteSpace(name) || name.StartsWith('@')) // "@%SystemRoot%\…" kaynak dizgileri
            {
                name = process.Description;
            }

            samples.Add(new SessionSample(pid, process.Exe, name, session.SimpleAudioVolume.Volume,
                session.SimpleAudioVolume.Mute, session.State == AudioSessionState.AudioSessionStateActive,
                IsSystemSounds: false));
        });
        _processes.Retain(pids); // oturumu kalmayan süreçler unutulur (Windows kimlikleri yeniden kullanır)
        return AppVolumeGrouping.Group(samples);
    }, []);

    /// <summary>Satırdaki tüm süreçlerin oturumlarına aynı düzeyi uygular.</summary>
    public void SetAppVolume(AppVolumeRow row, float volume) => Write(() => ForEachSession(session =>
    {
        if (row.ProcessIds.Contains((int)session.GetProcessID) && !session.IsSystemSoundsSession)
        {
            session.SimpleAudioVolume.Volume = Math.Clamp(volume, 0f, 1f);
        }
    }));

    /// <summary>Satırdaki uygulamanın exe yolu (simgesi için); bulunamazsa null.</summary>
    public string? ExePathFor(AppVolumeRow row) =>
        row.ProcessIds.Select(pid => _processes.Get(pid)?.Path).FirstOrDefault(p => p is not null);

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

    /// <summary>Etkin kayıt cihazları; konsol rolündeki varsayılan işaretli.</summary>
    public IReadOnlyList<AudioOutput> Inputs() => Read<IReadOnlyList<AudioOutput>>(() =>
    {
        var defaultId = _micConsole?.ID;
        var inputs = new List<AudioOutput>();
        foreach (var device in _enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active))
        {
            using (device)
            {
                inputs.Add(new AudioOutput(device.ID, device.FriendlyName, device.ID == defaultId));
            }
        }

        return inputs;
    }, []);

    /// <summary>Kayıt cihazını üç rolün varsayılanı yapar (Ada'nın "giriş cihazı seç" işlevi).</summary>
    public void SetDefaultInput(string deviceId) => Write(() => PolicyConfig.SetDefaultEndpoint(deviceId));

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Write(() => _enumerator.UnregisterEndpointNotificationCallback(_notifications));
        DetachOutput();
        DetachMic();
        _enumerator.Dispose();
    }

    /// <summary>Varsayılan çıkışın oturumlarını gezer; her oturum sarmalayıcısı iş bitince bırakılır.</summary>
    private void ForEachSession(Action<AudioSessionControl> visit)
    {
        if (_output is null)
        {
            return;
        }

        var manager = _output.AudioSessionManager;
        manager.RefreshSessions();
        var sessions = manager.Sessions;
        for (var i = 0; i < sessions.Count; i++)
        {
            using var session = sessions[i];
            visit(session);
        }
    }


    /// <summary>Konsol ve (farklıysa) iletişim rolündeki varsayılan kayıt cihazlarını bağlar.</summary>
    private void AttachMic()
    {
        DetachMic();
        Write(() =>
        {
            if (_enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Console))
            {
                _micConsole = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Console);
                _micConsole.AudioEndpointVolume.OnVolumeNotification += OnMicVolume;
            }

            if (_enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications))
            {
                var communications = _enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                if (communications.ID == _micConsole?.ID)
                {
                    communications.Dispose();
                }
                else
                {
                    _micCommunications = communications;
                    _micCommunications.AudioEndpointVolume.OnVolumeNotification += OnMicVolume;
                }
            }
        });
    }

    private void DetachMic()
    {
        foreach (var device in new[] { _micConsole, _micCommunications })
        {
            if (device is not null)
            {
                Write(() =>
                {
                    device.AudioEndpointVolume.OnVolumeNotification -= OnMicVolume;
                    device.Dispose();
                });
            }
        }

        _micConsole = null;
        _micCommunications = null;
    }

    /// <summary>Core Audio iş parçacığından gelir.</summary>
    private void OnMicVolume(AudioVolumeNotificationData data) => _dispatcher.BeginInvoke(() =>
    {
        if (!_disposed)
        {
            MicChanged?.Invoke(this, EventArgs.Empty);
        }
    });

    /// <summary>Varsayılan çıkışı (multimedya rolü) bağlar; önceki cihazın aboneliği bırakılır.</summary>
    private void AttachOutput()
    {
        DetachOutput();
        var generation = ++_outputGeneration;
        AudioEndpointVolumeNotificationDelegate handler = data => OnOutputVolume(data, generation);
        _outputHandler = handler;
        Write(() =>
        {
            if (!_enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia))
            {
                return;
            }

            _output = _enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia);
            _outputVolume = _output.AudioEndpointVolume;
            _outputVolume.NotificationGuid = OwnContext; // CamAda'nın yazdıkları bu bağlamla bildirilir
            _outputVolume.OnVolumeNotification += handler;
        });

        if (_outputVolume is null)
        {
            DetachOutput();
        }
    }

    private void DetachOutput()
    {
        if (_outputVolume is { } endpoint && _outputHandler is { } handler)
        {
            endpoint.OnVolumeNotification -= handler;
        }

        _outputVolume = null;
        _outputHandler = null;
        var output = _output;
        _output = null;
        Write(() => output?.Dispose()); // AudioEndpointVolume'u ve geri çağrı kaydını da bırakır
    }

    /// <summary>Core Audio iş parçacığından gelir; bağlandığı cihaz artık varsayılan değilse yok sayılır.</summary>
    private void OnOutputVolume(AudioVolumeNotificationData data, int generation)
    {
        var change = new MasterVolumeChange(data.MasterVolume, data.Muted, data.EventContext == OwnContext);
        _dispatcher.BeginInvoke(() =>
        {
            if (!_disposed && generation == _outputGeneration)
            {
                MasterChanged?.Invoke(this, change);
            }
        });
    }

    /// <summary>
    /// Cihaz bildirimi (başka iş parçacığından): UI'da yeniden bağlanır ve olaylar yayımlanır. Kayıt cihazları her
    /// bildirimde yeniden bağlanır (takılıp çıkarılan USB mikrofon, değişen varsayılan).
    /// </summary>
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

        AttachMic();
        MicChanged?.Invoke(this, EventArgs.Empty);
        DevicesChanged?.Invoke(this, EventArgs.Empty);
    });

    private static T Read<T>(Func<T> read, T fallback)
    {
        try
        {
            return read();
        }
        catch (Exception ex) when (CoreAudioErrors.IsRecoverable(ex))
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
        catch (Exception ex) when (CoreAudioErrors.IsRecoverable(ex))
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
