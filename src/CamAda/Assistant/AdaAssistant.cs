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
    AudioService? Audio);

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
    private const double BargeInRms = 0.04;
    private const int BargeInChunks = 2; // mikrofon parçaları ~100 ms: art arda iki parça ≥ 150 ms
    private const double DuckFactor = 0.3;

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

    public AdaAssistant(Dispatcher dispatcher, AdaDependencies deps)
    {
        _dispatcher = dispatcher;
        _deps = deps;
        _runner = new AdaToolRunner(deps.Tools with { EndSessionAfterSpeech = RequestEndAfterSpeech, LookupUsed = OnLookup },
            TimeProvider.System);
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

    public bool IsSessionActive => _session is not null;

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

    /// <summary>Kısayol ya da düğme: oturum yoksa açar, varsa kapatır. Basılı tutma ve çift basış tek oturum açar.</summary>
    public void Trigger()
    {
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
    }

    // --- Uyandırma ---

    private bool ShouldListen()
    {
        if (!_running || _disposed || _session is not null)
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

        // Ses cihazlarına dokunan her şey (çözme, WASAPI hoparlörü kurma ~0,3 sn) arka planda, tek COM dairesinde (MTA):
        // NAudio'nun cihaz nesneleri daireler arasında geçemiyor (E_NOINTERFACE); arayüze yalnız kimlik ve dizinler gelir.
        // Bu sırada uyandırma dinleyicisi kayda devam eder; ön kayıt mikrofon açılmadan hemen önce alınır, söz arada kopmaz.
        var audioSetup = await Task.Run(() => PrepareAudio(settings.Microphone));
        if (session.Closing)
        {
            DisposeInBackground(audioSetup.Speaker);
            return;
        }

        var preRoll = fromWake ? _listener?.DrainPreRoll() : null;
        StopListening();
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
            session.Pending.Add(preRoll);
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
            client.Received += e => _dispatcher.BeginInvoke(() => OnEvent(session, e));
            client.Closed += error => _dispatcher.BeginInvoke(() => OnClientClosed(session, error));

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

    private PlainMic? CreatePlainMic(string micId)
    {
        if (MicDevices.ById(micId) is not { } device)
        {
            return null;
        }

        try
        {
            var mic = new PlainMic(device);
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
            mic = new EchoCancelledMic(capture, render);
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

    private void NotifyHalfDuplex(Session session)
    {
        if (!session.HalfDuplexNoticeShown)
        {
            session.HalfDuplexNoticeShown = true;
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
        else if (adaTalking && Pcm.Rms(samples) > BargeInRms)
        {
            // Yerel söz kesme: Google'ın "kesildi"sini beklemeden sus; o yanıtın kalan sesi atılır.
            if (++session.LoudChunks >= BargeInChunks)
            {
                session.Speaker!.Clear();
                session.DropAudio = true;
                session.ModelSpeaking = false;
                session.LoudChunks = 0;
            }
        }
        else
        {
            session.LoudChunks = 0;
        }

        if (!session.Connected)
        {
            session.Pending.Add(toSend);
            return;
        }

        _ = session.Client?.SendAudioAsync(toSend);
    }

    private void OnEvent(Session session, LiveEvent e)
    {
        if (session != _session || session.Closing)
        {
            return;
        }

        switch (e)
        {
            case LiveEvent.InputText input:
                session.Rules.UserSpoke();
                _runner.UserSpoke();
                if (session.UserTurnDone)
                {
                    session.UserLine = "";
                    session.UserTurnDone = false;
                }

                session.UserLine += input.Text;
                SetState(new AdaState.Listening(session.UserLine.Trim()));
                break;
            case LiveEvent.AudioChunk audio:
                if (session.DropAudio)
                {
                    break;
                }

                session.Speaker?.Add(audio.Pcm24k);
                session.ModelSpeaking = true;
                session.TurnDone = false;
                session.AudioAfterEndRequest |= session.EndRequested;
                session.Rules.AdaSpeaking();
                if (State is not AdaState.Speaking)
                {
                    SetState(new AdaState.Speaking(session.AdaLine.Trim()));
                }

                break;
            case LiveEvent.OutputText output:
                if (session.AdaTurnDone)
                {
                    session.AdaLine = "";
                    session.AdaTurnDone = false;
                }

                session.AdaLine += output.Text;
                if (!session.DropAudio)
                {
                    SetState(new AdaState.Speaking(session.AdaLine.Trim()));
                }

                break;
            case LiveEvent.Interrupted:
                session.Speaker?.Clear();
                session.ModelSpeaking = false;
                session.DropAudio = false;
                session.TurnDone = true;
                session.AdaTurnDone = true;
                break;
            case LiveEvent.TurnComplete:
                session.TurnDone = true;
                session.DropAudio = false;
                session.AdaTurnDone = true;
                session.UserTurnDone = true;
                break;
            case LiveEvent.ToolCalls calls:
                _ = RunToolsAsync(session, calls.Calls);
                break;
            case LiveEvent.ToolCancelled cancelled:
                session.CancelledTools.UnionWith(cancelled.Ids);
                break;
            case LiveEvent.Usage usage:
                session.Cost += CostTable.Live(usage.Report, DateOnly.FromDateTime(DateTime.Now));
                break;
            case LiveEvent.GoAway:
                _ = CloseAsync(session);
                break;
        }
    }

    private async Task RunToolsAsync(Session session, IReadOnlyList<ToolCall> calls)
    {
        session.Rules.ToolRunning(true);
        var results = new List<ToolResult>();
        try
        {
            foreach (var call in calls)
            {
                results.Add(new ToolResult(call.Id, call.Name, await _runner.RunAsync(call)));
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

    private void OnLookup(LookupResult result)
    {
        if (_session is not { } session)
        {
            return;
        }

        var before = (_deps.Usage.Current?.Searches ?? 0) + session.Searches;
        var searches = result.Searched ? 1 : 0;
        session.Cost += CostTable.Lookup(result.PromptTokens, result.OutputTokens, before, searches);
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

        var playing = session.Speaker?.IsPlaying == true;
        if (session.TurnDone && !playing)
        {
            session.TurnDone = false;
            session.ModelSpeaking = false;
            session.Rules.AdaFinished();
            if (session.EndRequested && session.AudioAfterEndRequest)
            {
                _ = CloseAsync(session); // veda bitti
                return;
            }
        }

        if (session.EndRequested && !session.AudioAfterEndRequest && !playing
            && DateTime.UtcNow - session.EndRequestedAt > GoodbyeWait)
        {
            _ = CloseAsync(session); // model veda etmedi
            return;
        }

        switch (session.Rules.Check())
        {
            case SessionEnd.MaxDuration:
                _ = CloseAsync(session, notice: "Uzun sürdü — yeniden çağırabilirsin");
                break;
            case SessionEnd.NoSpeech or SessionEnd.Silence:
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

        public CancellationTokenSource Cancel { get; } = new();

        public GeminiLiveClient? Client { get; set; }

        public string? MicId { get; set; }

        public IAdaMic? Mic { get; set; }

        public AdaSpeaker? Speaker { get; set; }

        public AudioDucker? Ducker { get; set; }

        /// <summary>Bağlantı hazır olmadan gelen mikrofon parçaları (ön kayıt dahil), sırayla.</summary>
        public List<short[]> Pending { get; } = [];

        public HashSet<string> CancelledTools { get; } = [];

        public bool Connected { get; set; }

        public bool Closing { get; set; }

        public bool HalfDuplex { get; set; }

        public bool HalfDuplexNoticeShown { get; set; }

        /// <summary>Hoparlörde modelin sesi var (bipler sayılmaz: söz kesme ve yarı çift yönlü yalnız buna bakar).</summary>
        public bool ModelSpeaking { get; set; }

        public DateTime LastAdaAudioAt { get; set; } = DateTime.MinValue;

        public int LoudChunks { get; set; }

        /// <summary>Yerel söz kesmeden sonra o yanıtın kalan sesi atılır (yeni tur ya da "kesildi" gelene kadar).</summary>
        public bool DropAudio { get; set; }

        public bool TurnDone { get; set; }

        public bool UserTurnDone { get; set; }

        public bool AdaTurnDone { get; set; }

        public string UserLine { get; set; } = "";

        public string AdaLine { get; set; } = "";

        public bool EndRequested { get; set; }

        public DateTime EndRequestedAt { get; set; }

        public bool AudioAfterEndRequest { get; set; }

        public decimal Cost { get; set; }

        public int Searches { get; set; }
    }
}
