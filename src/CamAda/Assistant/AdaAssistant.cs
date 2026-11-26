using System.Runtime.InteropServices;
using System.Windows.Threading;
using CamAda.Assistant.Audio;
using CamAda.Audio;
using CamAda.Core.Assistant;
using CamAda.Core.Audio;
using CamAda.Core.Settings;
using NAudio.CoreAudioApi;

namespace CamAda.Assistant;

/// <summary>Ada'nın dışarıdan aldığı her şey.</summary>
internal sealed record AdaDependencies(
    Func<AssistantSettings> Settings,
    GeminiKeyStore Keys,
    AdaToolContext Tools,
    MemoryFile Memory,
    UsageFile Usage,
    string ModelDir,
    Action<Exception> Log,
    AudioService? Audio)
{
    /// <summary>Kullanıcının ses izi (misafir kipi); null ise ses tanınmaz, kip yalnız elle açılır.</summary>
    public VoiceprintFile? Voiceprint { get; init; }

    /// <summary>Konuşmacı ve konuşma algılayıcı modellerinin klasörü.</summary>
    public string VoiceModelDir { get; init; } = VoiceModels.Dir;
}

/// <summary>
/// Ada'nın oturum yöneticisi (tasarım "Uyanma ve oturum"): boştayken "Hey Ada" dinler; tetiklenince yankı engelleyicili
/// mikrofonu açar, Gemini Live'a bağlanır, sesi ve işlevleri yürütür; sessizlik, veda, düğme ya da hata ile kapanır ve
/// yeniden dinlemeye döner. Her şey arayüz iş parçacığında yürür; ses ve ağ olayları oraya aktarılır. Eski oturumdan gelen
/// geç olaylar oturum nesnesi karşılaştırılarak atılır.
/// </summary>
internal sealed class AdaAssistant : IDisposable
{
    private static readonly TimeSpan FailedShownFor = TimeSpan.FromSeconds(4);
    private static readonly TimeSpan CheckInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan HalfDuplexTail = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan TriggerDebounce = TimeSpan.FromMilliseconds(400);
    private static readonly TimeSpan GoodbyeWait = TimeSpan.FromSeconds(3);
    private static readonly TimeSpan MaxDurationGoodbyeCap = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan BargeInQuiet = TimeSpan.FromMilliseconds(700);
    private static readonly TimeSpan BargeInMaxHold = TimeSpan.FromSeconds(3);
    private const double BargeInRms = 0.04;
    private const double NoteVoiceRms = 0.03; // not kipinde kullanıcı konuşuyor sayılır (yerel ölçüm, canlı yazıdan önce)
    private const int BargeInChunks = 2; // mikrofon parçaları ~100 ms: art arda iki parça ≥ 150 ms
    private const double DuckFactor = 0.3;
    private const string MaxDurationNotice = "Uzun sürdü — yeniden çağırabilirsin";

    private readonly Dispatcher _dispatcher;
    private readonly AdaDependencies _deps;
    private readonly AdaToolRunner _runner;
    private readonly DispatcherTimer _check;
    private readonly DispatcherTimer _failedTimer;
    private WakeWordListener? _listener;
    private bool _listenerLoading;
    private Session? _session;
    private bool _running;
    private bool _fullScreen;
    private bool _disposed;
    private DateTime _lastTrigger = DateTime.MinValue;
    private bool _micFailureShown;
    private readonly HashSet<string> _halfDuplexNoticed = []; // yankı engelleyicisi açılamayan mikrofonlar (bilgi bir kez)
    private readonly GuestMode _guest;
    private SherpaVoiceAnalyzer? _voice;
    private Task? _voiceLoad;
    private bool _guestShown;
    private bool _micReserved;

    public AdaAssistant(Dispatcher dispatcher, AdaDependencies deps)
    {
        _dispatcher = dispatcher;
        _deps = deps;
        _guest = new GuestMode(TimeProvider.System, TimeSpan.FromMinutes(deps.Settings().GuestModeMinutes));
        _runner = new AdaToolRunner(deps.Tools with
        {
            EndSessionAfterSpeech = RequestEndAfterSpeech,
            LookupUsed = OnLookup,
            SetGuestMode = SetGuestManual,
        }, TimeProvider.System);
        _check = new DispatcherTimer(CheckInterval, DispatcherPriority.Normal, (_, _) => OnCheck(), dispatcher);
        _check.Stop();
        _failedTimer = new DispatcherTimer(FailedShownFor, DispatcherPriority.Normal, (_, _) => OnFailedShown(), dispatcher);
        _failedTimer.Stop();
        if (deps.Audio is { } audio)
        {
            audio.DevicesChanged += OnDevicesChanged;
        }
    }

    public AdaState State { get; private set; } = new AdaState.Idle();

    /// <summary>Durum değişti (arayüz iş parçacığında).</summary>
    public event EventHandler<AdaState>? StateChanged;

    /// <summary>Bir kez gösterilecek bilgi ("yankı engelleyici açılamadı" gibi; arayüz iş parçacığında).</summary>
    public event EventHandler<string>? Notice;

    /// <summary>Yalnız Debug denemeleri için iç olaylar (ön kayıt, söz kesme…); Release'te hiç yayımlanmaz.</summary>
    internal event Action<string>? Trace;

    public bool IsSessionActive => _session is not null;

    /// <summary>Misafir kipi açık mı (adada simge; Plan 8).</summary>
    public bool IsGuestMode => _guest.IsOn;

    /// <summary>Misafir kipi açıldı ya da kapandı (arayüz iş parçacığında, yalnız değişince).</summary>
    public event EventHandler<bool>? GuestModeChanged;

    /// <summary>Ses tanıma yüklendi mi (deneme için).</summary>
    internal bool IsVoiceReady => _voice is not null;

    /// <summary>"Hey Ada" dinleniyor mu (tanılama ve deneme için).</summary>
    internal bool IsWakeListening => _listener?.IsListening == true;

    /// <summary>Asistan etkinse ve anahtar varsa "Hey Ada" dinlemeye başlar.</summary>
    public void Start()
    {
        _running = true;
        UpdateListening();
    }

    /// <summary>Mikrofon seçimi ya da anahtar değişti: oturum yoksa dinleme yeniden kurulur (ya da artık gerekmiyorsa durur).</summary>
    public void Refresh()
    {
        if (_session is not null)
        {
            return;
        }

        StopListening();
        UpdateListening();
    }

    /// <summary>Oturumu hemen kapatır ve dinlemeyi bırakır (kilit, uyku, asistan kapatma).</summary>
    public void Stop()
    {
        _running = false;
        if (_session is { Closing: false } session)
        {
            _ = CloseAsync(session, immediate: true);
        }

        StopListening();
    }

    /// <summary>
    /// Sesini tanıtma mikrofonu kullanırken (Ada paneli): "Hey Ada" dinlemesi durur, kısayol ve uyanma oturum açmaz. Oturum
    /// sürüyorsa ya da başka bir tanıtma varsa null. Dönen nesne bırakılınca dinleme geri gelir.
    /// </summary>
    public IDisposable? ReserveMic()
    {
        if (_session is not null || _micReserved)
        {
            return null;
        }

        _micReserved = true;
        StopListening();
        return new MicReservation(this);
    }

    private sealed class MicReservation(AdaAssistant owner) : IDisposable
    {
        private bool _released;

        public void Dispose()
        {
            if (_released)
            {
                return;
            }

            _released = true;
            owner._micReserved = false;
            owner.UpdateListening();
        }
    }

    /// <summary>Kısayol ya da düğme: oturum yoksa açar, varsa kapatır. Basılı tutma ve çift basış tek oturum açar.</summary>
    public void Trigger()
    {
        if (_micReserved)
        {
            return; // ses tanıtılıyor: mikrofon tanıtmada
        }

        var now = DateTime.UtcNow;
        if (now - _lastTrigger < TriggerDebounce)
        {
            return;
        }

        if (_session is { } session)
        {
            if (!session.Closing)
            {
                _ = CloseAsync(session, immediate: true);
            }
        }
        else
        {
            _ = OpenAsync(fromWake: false);
        }

        // Süzgeç, açılışın arayüzü meşgul ettiği süreden sonra başlar: kuyrukta bekleyen ikinci basış da yok sayılır.
        _lastTrigger = DateTime.UtcNow;
    }

    /// <summary>Tam ekranda (ayar kapalıysa) uyandırma durur; kısayol çalışmaya devam eder.</summary>
    public void SetFullScreen(bool fullScreen)
    {
        _fullScreen = fullScreen;
        UpdateListening();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        Stop();
        _disposed = true;
        _check.Stop();
        _failedTimer.Stop();
        if (_deps.Audio is { } audio)
        {
            audio.DevicesChanged -= OnDevicesChanged;
        }

        _listener?.Dispose();
        _listener = null;
        _session?.Filter?.Dispose(); // süzgeç iş parçacığı çözümleyiciyi bıraksın
        _voice?.Dispose();
        _voice = null;
    }

    // --- Misafir kipi (Plan 8) ---

    /// <summary>
    /// Ses tanıma modelleri Ada açıkken bir kez, arka planda yüklenir (~30 MB); kapalıyken bellek tutulmaz. Yüklenemezse misafir
    /// kipi kullanılamaz, Ada bugünkü gibi çalışır.
    /// </summary>
    private Task EnsureVoice() => _voiceLoad ??= LoadVoiceAsync();

    private async Task LoadVoiceAsync()
    {
        try
        {
            var started = DateTime.UtcNow;
            var voice = await Task.Run(() => SherpaVoiceAnalyzer.TryCreate(_deps.VoiceModelDir, _deps.ModelDir, _deps.Log));
            TraceDebug($"ses tanıma {(voice is null ? "yüklenemedi" : "yüklendi")} ({(DateTime.UtcNow - started).TotalMilliseconds:F0} ms)");
            if (voice is null)
            {
                _deps.Log(new System.IO.FileNotFoundException("Misafir kipi: ses tanıma modelleri yüklenemedi", _deps.VoiceModelDir));
            }
            else if (_disposed)
            {
                voice.Dispose();
            }
            else
            {
                _voice = voice;
            }
        }
        catch (Exception ex) when (!IsFatal(ex))
        {
            _deps.Log(ex);
        }
    }

    /// <summary>Kullanıcı sesle açtı/kapattı (misafir_kipi işlevi); ses tanıma yoksa false.</summary>
    internal bool SetGuestManual(bool on)
    {
        if (_voice is null)
        {
            return false;
        }

        _guest.SetManual(on);
        UpdateGuest();
        return true;
    }

    /// <summary>Kip değiştiyse (açıldı, elle kapandı, 2 dk doldu) adaya bildirir.</summary>
    private void UpdateGuest()
    {
        var on = _guest.IsOn;
        if (on != _guestShown)
        {
            _guestShown = on;
            GuestModeChanged?.Invoke(this, on);
        }
    }

    /// <summary>
    /// Ses tanıma varsa oturumun süzgeci kurulur: ses izi ve eşik o anki ayardan. İlk söz "Ada" demeden geçer: "Hey Ada" da
    /// kısayol da Ada'ya seslenmektir.
    /// </summary>
    private void StartFilter(Session session, AssistantSettings settings)
    {
        if (_voice is not { } voice)
        {
            return;
        }

        voice.Reset();
        voice.Voiceprint = _deps.Voiceprint?.Load(VoiceModels.SpeakerModel);
        voice.Threshold = settings.SpeakerThreshold;
        session.Filter = new VoiceFilter(new VoiceGate(voice, new VoiceGateOptions()),
            output => _dispatcher.BeginInvoke(() => OnFiltered(session, output)), _deps.Log);
        session.Filter.AllowNextWithoutName(TimeSpan.FromSeconds(settings.NoSpeechSeconds));
        TraceDebug($"süzgeç: ses izi {(voice.Voiceprint is null ? "yok" : "var")}, misafir kipi {(_guest.IsOn ? "açık" : "kapalı")}");
    }

    /// <summary>Google'a gidecek her ses (ön kayıt, köprü, mikrofon) buradan: süzgeç varsa ondan geçer.</summary>
    private void Send(Session session, short[] samples)
    {
        if (session.Filter is { } filter)
        {
            filter.Post(samples, new GateInput(_guest.IsOn, _runner.Notes.Active));
            return;
        }

        NoteVoice(samples);
        Queue(session, samples);
    }

    /// <summary>Süzgeçten çıkan ses (arayüz iş parçacığında): Google'a gider; başka ses duyulduysa kip açılır.</summary>
    private void OnFiltered(Session session, GateOutput output)
    {
        if (session != _session || session.Closing)
        {
            return; // kapanan oturumun geç gelen sesi gitmez, kipi açmaz
        }

        var adaTalking = session.ModelSpeaking && session.Speaker?.IsPlaying == true;
        var bargeIn = _guest.IsOn && !session.HalfDuplex;
        foreach (var chunk in output.Send)
        {
            Queue(session, chunk);
            NoteVoice(chunk);
            if (bargeIn)
            {
                // Kipte yerel söz kesme yalnız süzgeçten geçen sesle (yani "Ada" ile): misafirin sesi Ada'yı susturmasın.
                ApplyBargeIn(session, session.BargeIn.OnMic(Pcm.Rms(chunk), adaTalking));
            }
        }

        if (output.OtherVoiceHeard)
        {
            TraceDebug("misafir sesi");
            if (_guest.OtherVoiceHeard())
            {
                session.Speaker?.PlayCue(AdaCue.Guest);
            }

            UpdateGuest();
        }
    }

    /// <summary>Google'ın canlı yazısı söz bitince geliyor: not, kullanıcı konuşurken sessizlik sayılıp bitmesin.</summary>
    private void NoteVoice(short[] sent)
    {
        if (_runner.Notes.Active && Pcm.Rms(sent) > NoteVoiceRms)
        {
            _runner.Notes.UserSpoke();
        }
    }

    private void ApplyBargeIn(Session session, BargeInAction action)
    {
        switch (action)
        {
            case BargeInAction.Hold:
                session.Speaker?.Hold();
                TraceDebug("yerel söz kesme: Ada tutuldu");
                break;
            case BargeInAction.Resume:
                session.Speaker?.Resume();
                TraceDebug("yerel söz kesme: Ada sürdü");
                break;
        }
    }

    // --- Uyandırma ---

    private bool ShouldListen()
    {
        if (!_running || _disposed || _session is not null || _micReserved)
        {
            return false;
        }

        var settings = _deps.Settings();
        return settings.Enabled && (!_fullScreen || settings.ListenInFullScreen) && _deps.Keys.Load() is not null;
    }

    /// <summary>Dinlemenin gerekip gerekmediğine bakar; hiçbir hata uygulamayı düşürmez (async void).</summary>
    private async void UpdateListening()
    {
        try
        {
            await UpdateListeningCoreAsync();
        }
        catch (Exception ex) when (!IsFatal(ex))
        {
            _deps.Log(ex);
        }
    }

    private async Task UpdateListeningCoreAsync()
    {
        if (!ShouldListen())
        {
            StopListening();
            return;
        }

        _ = EnsureVoice();
        if (_listener is null)
        {
            if (_listenerLoading)
            {
                return;
            }

            _listenerLoading = true;
            var settings = _deps.Settings();
            try
            {
                // Model yüklemesi (~5 MB) arayüzü bekletmesin.
                _listener = await Task.Run(() => new WakeWordListener(_deps.ModelDir, settings.WakeThreshold,
                    () => MicDevices.ResolveMic(_deps.Settings().Microphone)));
                _listener.Detected += () => _dispatcher.BeginInvoke(OnWake);
                _listener.Failed += message => _dispatcher.BeginInvoke(() => OnListenerFailed(message));
            }
            catch (Exception ex) when (!IsFatal(ex))
            {
                _deps.Log(ex);
                Notice?.Invoke(this, "Ada'nın uyandırma sözcüğü yüklenemedi; kısayol çalışıyor");
                return;
            }
            finally
            {
                _listenerLoading = false;
            }

            if (_disposed)
            {
                _listener.Dispose();
                _listener = null;
                return;
            }
        }

        if (!ShouldListen() || _listener.IsListening)
        {
            return;
        }

        bool started;
        try
        {
            started = _listener.Start();
        }
        catch (Exception ex) when (!IsFatal(ex))
        {
            // Ör. Windows gizlilik ayarı masaüstü uygulamalarına mikrofonu kapatmış (E_ACCESSDENIED → UnauthorizedAccessException).
            _deps.Log(ex);
            started = false;
        }

        if (started)
        {
            _micFailureShown = false;
        }
        else
        {
            ShowMicFailureOnce();
        }
    }

    /// <summary>"Mikrofon bulunamadı" bir kez görünür; mikrofon yeniden dinlenene kadar (tam ekrandan çıkış, cihaz olayları) tekrarlanmaz.</summary>
    private void ShowMicFailureOnce()
    {
        if (!_micFailureShown)
        {
            _micFailureShown = true;
            ShowFailed(AssistantError.Microphone, AdaState.MessageFor(AssistantError.Microphone, false));
        }
    }

    [System.Diagnostics.Conditional("DEBUG")]
    private void TraceDebug(string text) => Trace?.Invoke(text);

    private static bool IsFatal(Exception ex) => ex is OutOfMemoryException or StackOverflowException or AccessViolationException;

    private void StopListening() => _listener?.Stop();

    private void OnWake()
    {
        if (_session is not null || !ShouldListen() || _listener is null)
        {
            return;
        }

        // "Hey Ada, sesi kıs" tek nefeste: son 2 sn bağlantı hazır olunca ilk gönderilir.
        _ = OpenAsync(fromWake: true);
    }

    private void OnListenerFailed(string message)
    {
        _deps.Log(new InvalidOperationException("Ada uyandırma mikrofonu: " + message));
        StopListening();
        if (_session is null)
        {
            ShowMicFailureOnce(); // mikrofon geri gelince cihaz olayıyla yeniden denenir
        }
    }

    // --- Oturum ---

    /// <param name="fromWake">"Hey Ada" ile açıldıysa ön kayıt ilk gönderilir; kısayolda gönderilmez.</param>
    private async Task OpenAsync(bool fromWake)
    {
        try
        {
            await OpenCoreAsync(fromWake);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Beklenmeyen hata oturumu "Bağlanıyor"da asılı bırakmasın: günlüğe yazılır, oturum hata ile kapanır.
            _deps.Log(ex);
            if (_session is { Closing: false } session)
            {
                await CloseAsync(session, failure: (AssistantError.Unknown, AdaState.MessageFor(AssistantError.Unknown, false)));
            }
        }
    }

    private async Task OpenCoreAsync(bool fromWake)
    {
        if (_disposed)
        {
            return;
        }

        var settings = _deps.Settings();
        if (_deps.Keys.Load() is not { } key)
        {
            ShowFailed(AssistantError.InvalidKey, "Gemini anahtarı yok — Ada ayarlarından ekle");
            return;
        }

        var session = new Session(settings, new SessionRules(TimeProvider.System, TimeSpan.FromSeconds(settings.SilenceSeconds),
            TimeSpan.FromSeconds(settings.NoSpeechSeconds), TimeSpan.FromMinutes(settings.MaxSessionMinutes)));
        _session = session;
        _failedTimer.Stop();
        _runner.ResetConfirmations(); // önceki oturumdan kalan "emin misin?" bu oturumda geçmez
        SetState(new AdaState.Connecting());

        // Ses tanıma yükleniyorsa (Ada yeni açıldı) mikrofon açılmadan biter: oturumun ilk sözü de süzgeçten geçsin.
        await EnsureVoice();
        if (session.Closing)
        {
            return;
        }

        StartFilter(session, settings);
        UpdateGuest(); // 2 dk içinde yeniden çağrıldıysa kip baştan açık gelir

        // "Hey Ada" ile: uyandırma sözünün sonundan (algılamadan 0,8 sn önce) bugüne kadar duyulan ilk gönderilir; öncesi
        // bilgisayarda kalır. Kısayolla: basıştan öncesi hiç gitmez. Dinleyici, oturum mikrofonu ilk sesini verene kadar kayda
        // devam eder (OnMicSamples aradaki sesi sıraya ekleyip onu durdurur): "Hey Ada, sesi kıs" tek nefeste söylenebilir.
        var preRoll = fromWake ? _listener?.DrainFromWake() : null;
        if (!fromWake)
        {
            _listener?.DrainPreRoll();
        }

        session.BridgeFromListener = _listener?.IsListening == true;
        TraceDebug($"ön kayıt {(preRoll?.Length ?? 0) / 16} ms, köprü {(session.BridgeFromListener ? "var" : "yok")}");

        // Ses cihazlarına dokunan her şey (çözme, WASAPI hoparlörü kurma ~0,3 sn) arka planda, tek COM dairesinde (MTA):
        // NAudio'nun cihaz nesneleri daireler arasında geçemiyor (E_NOINTERFACE); arayüze yalnız kimlik ve dizinler gelir.
        var audioSetup = await Task.Run(() => PrepareAudio(settings.Microphone));
        if (session.Closing)
        {
            DisposeInBackground(audioSetup.Speaker);
            return;
        }

        if (audioSetup.MicId is null)
        {
            DisposeInBackground(audioSetup.Speaker);
            await CloseAsync(session, failure: (AssistantError.Microphone, AdaState.MessageFor(AssistantError.Microphone, false)));
            return;
        }

        if (audioSetup.Speaker is not { } output)
        {
            await CloseAsync(session, failure: (AssistantError.Unknown, "Hoparlör bulunamadı"));
            return;
        }

        session.Speaker = output;
        output.PlayCue(AdaCue.Wake);
        if (settings.DuckOthers)
        {
            session.Ducker = new AudioDucker(output.DeviceId);
            session.Ducker.Duck(DuckFactor);
        }

        if (preRoll is { Length: > 0 })
        {
            Send(session, preRoll); // 1. durum tam da ilk komutta oluyor: ön kayıt da süzgeçten geçer
        }

        session.MicId = audioSetup.MicId;
        await StartMicAsync(session, audioSetup.Capture, audioSetup.Render, tryEchoCancel: true);
        if (session.Closing)
        {
            return;
        }

        session.Rules.Started();
        _check.Start();

        try
        {
            var setup = LiveSetup.Build(new LiveSetupOptions(settings.Model, settings.Voice,
                AdaInstruction.Build(DateTime.Now, _deps.Memory.Snapshot()), AdaTools.Declarations()));
            var client = await GeminiLiveClient.ConnectAsync(key, setup, session.Cancel.Token);
            if (session.Closing)
            {
                await client.DisposeAsync();
                return;
            }

            session.Client = client;
            client.Attach(e => _dispatcher.BeginInvoke(() => OnEvent(session, e)),
                error => _dispatcher.BeginInvoke(() => OnClientClosed(session, error)));

            // Bağlantı kurulurken biriken ses (ön kayıt dahil) sırayla gider; bu arada gelen parçalar da kuyruğa eklenir.
            while (session.Pending.Count > 0 && !session.Closing)
            {
                var chunk = session.Pending[0];
                session.Pending.RemoveAt(0);
                await client.SendAudioAsync(chunk);
            }

            session.Connected = true;
            if (State is AdaState.Connecting)
            {
                SetState(new AdaState.Listening(null));
            }
        }
        catch (AssistantException ex)
        {
            await CloseAsync(session, failure: (ex.Error, AdaState.MessageFor(ex.Error, midSession: false)));
        }
        catch (OperationCanceledException)
        {
            // kullanıcı bağlanırken kapattı
        }
    }

    /// <summary>Arka planda (MTA): mikrofon ve hoparlör çözülür, hoparlör kurulur, yankı engelleyici dizinleri bulunur.</summary>
    private AudioSetup PrepareAudio(string? micHint)
    {
        var mic = MicDevices.ResolveMic(micHint);
        var speaker = MicDevices.DefaultSpeaker();
        if (mic is null || speaker is null)
        {
            return new AudioSetup(mic?.ID, null, -1, -1);
        }

        var (capture, render) = MicDevices.DspIndexes(mic, speaker);
        return new AudioSetup(mic.ID, CreateSpeaker(speaker), capture, render);
    }

    private AdaSpeaker? CreateSpeaker(MMDevice device)
    {
        try
        {
            return new AdaSpeaker(device);
        }
        catch (Exception ex) when (!IsFatal(ex))
        {
            _deps.Log(ex);
            return null;
        }
    }

    private IAdaMic? CreatePlainMic(string micId)
    {
        if (MicDevices.ById(micId) is not { } device)
        {
            return null;
        }

        try
        {
            var mic = MicFactory.Plain(device);
            mic.Start();
            return mic;
        }
        catch (Exception ex) when (!IsFatal(ex))
        {
            _deps.Log(ex);
            return null;
        }
    }

    private static void DisposeInBackground(IDisposable? disposable)
    {
        if (disposable is not null)
        {
            _ = Task.Run(disposable.Dispose);
        }
    }

    /// <summary>
    /// Yankı engelleyiciyi dener (kendi MTA iş parçacığını açar); açılamazsa (ya da istenmezse) düz mikrofon (arka planda
    /// kurulur ve başlatılır) ve yarı çift yönlü.
    /// </summary>
    private async Task StartMicAsync(Session session, int capture, int render, bool tryEchoCancel)
    {
        IAdaMic mic;
        if (tryEchoCancel && capture >= 0 && render >= 0)
        {
            mic = MicFactory.EchoCancelled(capture, render);
            Wire(session, mic);
            mic.Start();
            return;
        }

        if (tryEchoCancel)
        {
            NotifyHalfDuplex(session);
        }

        var micId = session.MicId!;
        if (await Task.Run(() => CreatePlainMic(micId)) is not { } plain)
        {
            _ = CloseAsync(session, failure: (AssistantError.Microphone, AdaState.MessageFor(AssistantError.Microphone, false)));
            return;
        }

        if (session.Closing || session != _session)
        {
            DisposeInBackground(plain);
            return;
        }

        Wire(session, plain); // başlatıldı; ilk parçalar arayüz kuyruğunda bu bağlantıdan sonra işlenir
    }

    private void Wire(Session session, IAdaMic mic)
    {
        mic.Samples += samples => _dispatcher.BeginInvoke(() => OnMicSamples(session, mic, samples));
        mic.Failed += message => _dispatcher.BeginInvoke(() => OnMicFailed(session, mic, message));
        session.Mic = mic;
        session.HalfDuplex = !mic.EchoCancelled;
    }

    /// <summary>
    /// Bilgi her mikrofon için, uygulama açık kaldıkça bir kez: yankı engelleyiciyi hiç açamayan mikrofonda her oturumda
    /// yinelenmez.
    /// </summary>
    private void NotifyHalfDuplex(Session session)
    {
        if (_halfDuplexNoticed.Add(session.MicId ?? ""))
        {
            Notice?.Invoke(this, "Yankı engelleyici açılamadı — Ada konuşurken araya giremezsin");
        }
    }

    private void OnMicFailed(Session session, IAdaMic mic, string message)
    {
        if (session != _session || session.Closing || session.Mic != mic)
        {
            return;
        }

        _deps.Log(new InvalidOperationException("Ada mikrofonu: " + message));
        session.Mic = null;
        DisposeInBackground(mic);
        if (mic.EchoCancelled)
        {
            NotifyHalfDuplex(session);
            _ = StartMicAsync(session, -1, -1, tryEchoCancel: false);
            return;
        }

        _ = CloseAsync(session, failure: (AssistantError.Microphone, AdaState.MessageFor(AssistantError.Microphone, false)));
    }

    private void OnMicSamples(Session session, IAdaMic mic, short[] samples)
    {
        if (session != _session || session.Closing || session.Mic != mic)
        {
            return;
        }

        if (session.BridgeFromListener)
        {
            // Oturum mikrofonunun ilk sesi: dinleyicinin o ana kadar duyduğu sıraya girer, dinleyici durur. Son parça kadarı
            // iki mikrofonda da var (aynı anı duydular): kırpılır.
            session.BridgeFromListener = false;
            var bridge = _listener?.DrainPreRoll() ?? [];
            StopListening();
            if (bridge.Length > samples.Length)
            {
                Send(session, bridge[..^samples.Length]);
            }

            TraceDebug($"köprü: dinleyiciden {Math.Max(0, bridge.Length - samples.Length) / 16} ms");
        }

        var now = DateTime.UtcNow;
        var adaTalking = session.ModelSpeaking && session.Speaker?.IsPlaying == true;
        if (adaTalking)
        {
            session.LastAdaAudioAt = now;
        }

        var toSend = samples;
        if (session.HalfDuplex)
        {
            // Yankı engelleyici yok: Ada konuşurken ve hemen sonrasında mikrofon sessiz gönderilir (kendi sesiyle kesilmesin).
            if (adaTalking || now - session.LastAdaAudioAt < HalfDuplexTail)
            {
                toSend = new short[samples.Length];
            }
        }
        else if (session.Filter is null || !_guest.IsOn)
        {
            // Yerel söz kesme: Google'ın "kesildi"sini beklemeden susar; Google onaylamazsa (öksürük) kaldığı yerden sürer.
            // Misafir kipinde süzgeçten geçen sesle yapılır (OnFiltered).
            ApplyBargeIn(session, session.BargeIn.OnMic(Pcm.Rms(samples), adaTalking));
        }

        // Yarı çift yönlüde Ada konuşurken toSend sıfırdır: Ada'nın kendi sesi ne nota ne süzgece (misafir sanılmasın) girer.
        Send(session, toSend);
    }

    /// <summary>Bağlantı hazır değilse sıraya, hazırsa doğrudan Google'a.</summary>
    private static void Queue(Session session, short[] samples)
    {
        if (!session.Connected)
        {
            session.Pending.Add(samples);
            return;
        }

        _ = session.Client?.SendAudioAsync(samples);
    }

    private void OnEvent(Session session, LiveEvent e)
    {
        if (e is LiveEvent.Usage usage)
        {
            // Kapanış sürerken ya da sonra gelen kullanım da harcamaya girer.
            AddCost(session, CostTable.Live(usage.Report, DateOnly.FromDateTime(DateTime.Now)), 0);
            return;
        }

        if (session != _session || session.Closing)
        {
            return;
        }

        switch (e)
        {
            case LiveEvent.InputText input:
                session.Rules.UserSpoke();
                _runner.UserSpoke();
                _runner.Notes.UserSpoke();
                session.Transcript.UserText(input.Text);
                SetState(new AdaState.Listening(session.Transcript.User.Trim()));
                break;
            case LiveEvent.AudioChunk audio:
                if (_runner.Notes.Muted)
                {
                    break; // not alınırken Ada konuşmaz (model yanıt üretse de duyulmaz)
                }

                session.Speaker?.Add(audio.Pcm24k);
                session.ModelSpeaking = true;
                session.TurnDone = false;
                session.AudioAfterEndRequest |= session.EndRequested;
                session.Rules.AdaSpeaking();
                session.Transcript.AdaAudio(); // yeni yanıt: satır bir önceki cümleyi göstermesin
                if (State is not AdaState.Speaking)
                {
                    SetState(new AdaState.Speaking(session.Transcript.Ada.Trim()));
                }

                break;
            case LiveEvent.OutputText output:
                if (_runner.Notes.Muted)
                {
                    break;
                }

                session.Transcript.AdaText(output.Text);
                SetState(new AdaState.Speaking(session.Transcript.Ada.Trim()));
                break;
            case LiveEvent.Interrupted:
                TraceDebug("Google: kesildi");
                session.Speaker?.Clear();
                session.BargeIn.Interrupted();
                session.ModelSpeaking = false;
                session.TurnDone = true;
                session.AudioAfterEndRequest = false; // kesilen yanıt veda değildi
                if (session.EndRequested && session.MaxReachedAt is null)
                {
                    // Kullanıcı vedanın sözünü kesti ("teşekkürler" sonrası "bir şey daha"): oturum sürer. 10 dk sınırında sürmez.
                    session.EndRequested = false;
                    TraceDebug("veda kesildi: oturum sürüyor");
                }

                session.Transcript.Interrupted();
                break;
            case LiveEvent.TurnComplete:
                session.TurnDone = true;
                session.Transcript.TurnComplete();
                break;
            case LiveEvent.ToolCalls calls:
                _ = RunToolsAsync(session, calls.Calls);
                break;
            case LiveEvent.ToolCancelled cancelled:
                session.CancelledTools.UnionWith(cancelled.Ids);
                break;
            case LiveEvent.GoAway:
                // Google bağlantıyı birazdan kapatacak: Ada'nın sesi bitince kapanır, adada neden yazar.
                _ = CloseAsync(session, notice: "Bağlantı sona erdi — yeniden çağırabilirsin");
                break;
        }
    }

    private async Task RunToolsAsync(Session session, IReadOnlyList<ToolCall> calls)
    {
        session.Rules.ToolRunning(true);
        var results = new List<ToolResult>();
        try
        {
            var names = calls.Select(c => c.Name).ToList();
            foreach (var call in calls)
            {
                // Bir not işleviyle aynı grupta gelen komut nottan gelmiştir ("…sesi kıs" dikte edildi): yürütülmez.
                var result = NoteSession.RefusedInBatch(names, call.Name) ? AdaToolRunner.NoteRefusal : await _runner.RunAsync(call);
                results.Add(new ToolResult(call.Id, call.Name, result));
                if (call.Name == AdaTools.NotAl && _runner.Notes.Active)
                {
                    TraceDebug("not başladı");
                }
            }
        }
        finally
        {
            session.Rules.ToolRunning(false);
        }

        if (session != _session || session.Closing || session.Client is not { } client)
        {
            return;
        }

        var keep = results.Where(r => r.Id is null || !session.CancelledTools.Contains(r.Id)).ToList();
        if (keep.Count > 0)
        {
            await client.SendToolResultsAsync(keep);
        }

        if (session.EndRequested)
        {
            session.EndRequestedAt = DateTime.UtcNow; // veda için bekleme sonuç gönderilince başlar
        }
    }

    private void RequestEndAfterSpeech()
    {
        if (_session is { } session)
        {
            session.EndRequested = true;
            session.EndRequestedAt = DateTime.UtcNow;
        }
    }

    /// <summary>Not 4 sn sessizlikle bitti: kaydedilir, Ada'nın sesi açılır ve model "Not aldım" der.</summary>
    private async Task FinishNoteAfterSilenceAsync(Session session)
    {
        try
        {
            if (_runner.FinishNote() && session == _session && !session.Closing && session.Client is { } client)
            {
                TraceDebug("not bitti (sessizlik)");
                await client.SendTextAsync(AdaInstruction.NoteFinished);
            }
        }
        catch (Exception ex) when (!IsFatal(ex))
        {
            _deps.Log(ex);
        }
    }

    /// <summary>Arama maliyeti; oturum o arada kapandıysa doğrudan bu ayın kaydına.</summary>
    private void OnLookup(LookupResult result)
    {
        var session = _session is { UsageRecorded: false } open ? open : null;
        var before = (_deps.Usage.Current?.Searches ?? 0) + (session?.Searches ?? 0);
        var searches = result.Searched ? 1 : 0;
        var cost = CostTable.Lookup(result.PromptTokens, result.OutputTokens, before, searches);
        if (session is null)
        {
            _deps.Usage.Add(DateOnly.FromDateTime(DateTime.Now), cost, 0, searches);
            return;
        }

        AddCost(session, cost, searches);
    }

    /// <summary>Oturumun harcaması kapanışta bir kez yazılır; ondan sonra gelen her kalem doğrudan eklenir.</summary>
    private void AddCost(Session session, decimal usd, int searches)
    {
        if (session.UsageRecorded)
        {
            _deps.Usage.Add(DateOnly.FromDateTime(DateTime.Now), usd, 0, searches);
            return;
        }

        session.Cost += usd;
        session.Searches += searches;
    }

    private void OnClientClosed(Session session, AssistantError error)
    {
        if (session != _session || session.Closing)
        {
            return;
        }

        _ = error == AssistantError.None
            ? CloseAsync(session)
            : CloseAsync(session, failure: (error, AdaState.MessageFor(error, midSession: true)));
    }

    private void OnCheck()
    {
        if (_session is not { Closing: false } session)
        {
            _check.Stop();
            return;
        }

        if (_runner.Notes.ShouldFinish())
        {
            _ = FinishNoteAfterSilenceAsync(session);
        }

        UpdateGuest(); // kendiliğinden açılan kip son başka sesten 2 dk sonra biter

        var playing = session.Speaker?.IsPlaying == true;
        if (session.TurnDone && !playing)
        {
            session.TurnDone = false;
            session.ModelSpeaking = false;
            session.Rules.AdaFinished();
            if (session.Filter is { } filter && (_runner.ConfirmPending || AdaText.EndsWithQuestion(session.Transcript.Ada)))
            {
                // Ada soru sordu: yanıt misafir kipinde de "Ada" demeden geçer (ses yine denetlenir).
                filter.AllowNextWithoutName(TimeSpan.FromSeconds(session.Settings.SilenceSeconds));
                TraceDebug("soru: sonraki söz adsız");
            }

            if (session.EndRequested && session.AudioAfterEndRequest)
            {
                _ = CloseAsync(session, notice: session.EndNotice); // veda bitti
                return;
            }

            if (State is AdaState.Speaking)
            {
                SetState(new AdaState.Listening(null)); // Ada sustu, dinliyor
            }
        }

        if (session.EndRequested && !session.AudioAfterEndRequest && !playing
            && DateTime.UtcNow - session.EndRequestedAt > GoodbyeWait)
        {
            _ = CloseAsync(session, notice: session.EndNotice); // model veda etmedi
            return;
        }

        switch (session.Rules.Check())
        {
            case SessionEnd.MaxDuration when session.MaxReachedAt is null:
                // Tasarım: Ada "uzun sürdü, yeniden çağırabilirsin" deyip kapanır (tam ekranda da duyulur).
                session.MaxReachedAt = DateTime.UtcNow;
                session.EndNotice = MaxDurationNotice;
                TraceDebug("en uzun süre: veda istendi");
                if (session.Client is { } client && session.Connected)
                {
                    session.EndRequested = true;
                    session.EndRequestedAt = DateTime.UtcNow;
                    _ = client.SendTextAsync(AdaInstruction.MaxDurationGoodbye);
                }
                else
                {
                    _ = CloseAsync(session, notice: MaxDurationNotice);
                }

                break;
            case SessionEnd.MaxDuration when DateTime.UtcNow - session.MaxReachedAt > MaxDurationGoodbyeCap:
                _ = CloseAsync(session, notice: MaxDurationNotice); // veda uzadı
                break;
            case SessionEnd.NoSpeech or SessionEnd.Silence when !_runner.Notes.Active:
                // Not sürerken bitişi not kipi yönetir (canlı yazı söz bitince geliyor; oturum sessizliği dikte ortasında kapatmasın).
                _ = CloseAsync(session);
                break;
        }
    }

    /// <summary>
    /// Her yoldan aynı kapanış: mikrofon durur, bağlantı kapanır, hoparlör bitince kısma geri alınır, kullanım yazılır,
    /// uyandırma yeniden başlar. <paramref name="immediate"/> ise (düğme, kısayol, kilit) çalan ses de kesilir.
    /// </summary>
    private async Task CloseAsync(Session session, (AssistantError Error, string Message)? failure = null, string? notice = null,
        bool immediate = false)
    {
        if (session.Closing)
        {
            return;
        }

        session.Closing = true;
        session.Cancel.Cancel();
        session.Speaker?.Resume(); // yerel söz kesmede tutulan ses kalmasın: mikrofon kapanınca onu bırakacak kimse yok
        if (immediate)
        {
            // Düğme, kısayol, kilit, uyku ya da uygulamadan çıkış: ses hemen kesilir, öbür uygulamaların sesi ilk beklemeden
            // önce geri gelir (çıkışta süreç bu metodun sonunu görmeyebilir).
            session.Speaker?.Clear();
            session.Ducker?.Restore();
        }

        if (failure is { } f)
        {
            session.Speaker?.Clear();
            session.Speaker?.PlayCue(AdaCue.Error);
            ShowFailed(f.Error, f.Message);
        }
        else if (notice is not null)
        {
            ShowFailed(AssistantError.None, notice);
        }

        try
        {
            _runner.FinishNote(); // not dosyası her parçada yazıldı

            if (session.Mic is { } mic)
            {
                session.Mic = null;
                await Task.Run(mic.Dispose);
            }

            if (session.Client is { } client)
            {
                session.Client = null;
                await client.DisposeAsync();
            }

            for (var i = 0; i < 80 && session.Speaker?.IsPlaying == true; i++)
            {
                await Task.Delay(100);
            }
        }
        catch (Exception ex) when (!IsFatal(ex))
        {
            _deps.Log(ex);
        }
        finally
        {
            // Ne olursa olsun oturum bırakılır: yoksa Ada bir daha açılmaz, uyandırma dinlemez.
            _runner.Notes.Reset();
            if (session.Filter is { } filter)
            {
                session.Filter = null;
                DisposeInBackground(filter); // kip durumu korunur: 2 dk içinde yeni oturum baştan misafir kipinde
            }

            session.Ducker?.Restore();
            if (session.Speaker is { } speaker)
            {
                session.Speaker = null;
                _ = Task.Run(() =>
                {
                    try
                    {
                        speaker.Dispose();
                    }
                    catch (Exception ex) when (!IsFatal(ex))
                    {
                        _deps.Log(ex); // ör. cihaz oturum sırasında çıkarıldı
                    }
                });
            }

            if (session.Connected)
            {
                _deps.Usage.Add(DateOnly.FromDateTime(DateTime.Now), session.Cost, 1, session.Searches);
            }

            session.UsageRecorded = true;

            if (_session == session)
            {
                _session = null;
            }

            if (State is not AdaState.Failed)
            {
                State = new AdaState.Idle();
            }

            StateChanged?.Invoke(this, State); // durum aynı kalsa da (hata satırı erken bittiyse) düğme ve satır oturumun bittiğini öğrenir
            UpdateListening();
        }
    }

    // --- Çıkış cihazı değişimi ---

    /// <summary>
    /// Oturum sürerken varsayılan çıkış değiştiyse ("kulaklığa geç", Buds bağlandı) Ada'nın sesi yeni cihazdan sürer;
    /// yankı engelleyici yeni referansla yeniden kurulur, olmazsa yarı çift yönlüye düşülür.
    /// </summary>
    private async void OnDevicesChanged(object? sender, EventArgs e)
    {
        try
        {
            await OnDevicesChangedCoreAsync();
        }
        catch (Exception ex) when (!IsFatal(ex))
        {
            _deps.Log(ex);
        }
    }

    private async Task OnDevicesChangedCoreAsync()
    {
        if (_session is null)
        {
            // Boşta: mikrofon sonradan takıldıysa ya da yeniden bağlandıysa uyandırma kendiliğinden geri gelir.
            if (_running && !_disposed && _listener?.IsListening != true)
            {
                UpdateListening();
            }

            return;
        }

        if (_session is not { Closing: false, Speaker: { } old } session)
        {
            return;
        }

        var micId = session.MicId;
        var change = await Task.Run(() =>
        {
            if (MicDevices.DefaultSpeaker() is not { } current || current.ID == old.DeviceId)
            {
                return null;
            }

            var (capture, render) = micId is null ? (-1, -1) : MicDevices.DspIndexes(micId, current.ID);
            return CreateSpeaker(current) is { } speaker ? new AudioSetup(micId, speaker, capture, render) : null;
        });
        if (change?.Speaker is not { } replacement)
        {
            return;
        }

        if (session != _session || session.Closing || session.Speaker != old)
        {
            DisposeInBackground(replacement);
            return;
        }

        session.Speaker = replacement;
        session.ModelSpeaking = false;
        DisposeInBackground(old);
        if (session.Mic is { EchoCancelled: true } mic)
        {
            session.Mic = null;
            await Task.Run(mic.Dispose); // eski yankı engelleyici mikrofonu bıraksın
            if (session == _session && !session.Closing)
            {
                await StartMicAsync(session, change.Capture, change.Render, tryEchoCancel: true);
            }
        }
    }

    // --- Durum ---

    private void SetState(AdaState state)
    {
        if (state == State)
        {
            return;
        }

        State = state;
        StateChanged?.Invoke(this, state);
    }

    private void ShowFailed(AssistantError error, string message)
    {
        SetState(new AdaState.Failed(error, message));
        _failedTimer.Stop();
        _failedTimer.Start();
    }

    private void OnFailedShown()
    {
        _failedTimer.Stop();
        if (State is AdaState.Failed)
        {
            SetState(_session is { Closing: false } ? new AdaState.Listening(null) : new AdaState.Idle());
        }
    }

    private sealed record AudioSetup(string? MicId, AdaSpeaker? Speaker, int Capture, int Render);

    private sealed class Session(AssistantSettings settings, SessionRules rules)
    {
        public AssistantSettings Settings { get; } = settings;

        public SessionRules Rules { get; } = rules;

        public BargeInGate BargeIn { get; } = new(TimeProvider.System, BargeInRms, BargeInChunks, BargeInQuiet, BargeInMaxHold);

        public SessionTranscript Transcript { get; } = new();

        /// <summary>Uyandırma dinleyicisi, oturum mikrofonunun ilk sesine kadar aradaki sesi tutuyor.</summary>
        public bool BridgeFromListener { get; set; }

        public CancellationTokenSource Cancel { get; } = new();

        public GeminiLiveClient? Client { get; set; }

        public string? MicId { get; set; }

        public IAdaMic? Mic { get; set; }

        public AdaSpeaker? Speaker { get; set; }

        public AudioDucker? Ducker { get; set; }

        /// <summary>Misafir kipi süzgeci (ses tanıma yüklendiyse).</summary>
        public VoiceFilter? Filter { get; set; }

        /// <summary>Bağlantı hazır olmadan gelen mikrofon parçaları (ön kayıt dahil), sırayla.</summary>
        public List<short[]> Pending { get; } = [];

        public HashSet<string> CancelledTools { get; } = [];

        public bool Connected { get; set; }

        public bool Closing { get; set; }

        public bool HalfDuplex { get; set; }

        /// <summary>Hoparlörde modelin sesi var (bipler sayılmaz: söz kesme ve yarı çift yönlü yalnız buna bakar).</summary>
        public bool ModelSpeaking { get; set; }

        public DateTime LastAdaAudioAt { get; set; } = DateTime.MinValue;

        public bool TurnDone { get; set; }

        public bool EndRequested { get; set; }

        public DateTime EndRequestedAt { get; set; }

        public bool AudioAfterEndRequest { get; set; }

        /// <summary>Veda ile kapanınca adada görünecek bilgi (10 dk sınırı).</summary>
        public string? EndNotice { get; set; }

        public DateTime? MaxReachedAt { get; set; }

        public decimal Cost { get; set; }

        public int Searches { get; set; }

        /// <summary>Harcama bu ayın kaydına yazıldı; bundan sonraki kalemler doğrudan eklenir.</summary>
        public bool UsageRecorded { get; set; }
    }
}
