using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CamAda.Assistant;
using CamAda.Audio;
using CamAda.Clips;
using CamAda.Panels;
using CamAda.Core.Apps;
using CamAda.Core.Assistant;
using CamAda.Core.Audio;
using CamAda.Core.Clipboard;
using CamAda.Core.Input;
using CamAda.Core.Network;
using CamAda.Core.Power;
using CamAda.Input;
using CamAda.Core.Diagnostics;
using CamAda.Core.Safety;
using CamAda.Core.Settings;
using CamAda.Core.Startup;
using CamAda.Interop;
using CamAda.Island;
using CamAda.Media;
using CamAda.QuickSettings;
using CamAda.Record;
using CamAda.Spotify;
using CamAda.Weather;
using CamAda.Safety;
using CamAda.Shell;
using CamAda.Startup;

namespace CamAda;

public partial class App : Application
{
    private static readonly string RoamingDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CamAda");

    private static readonly string LocalDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CamAda");

    private Mutex? _singleInstance;
    private ShellHost? _shell;
    private TaskbarGuard? _guard;
    private StripAppBar? _strip;
    private IslandWindow? _island;
    private Process? _watchdog;
    private AutostartService? _autostart;
    private DockViewModel? _dock;
    private NowPlayingService? _media;
    private WeatherService? _weather;
    private RecordWindow? _record;
    private SpotifyClient? _spotify;
    private ISpotifyExtras? _spotifyExtras;
    private AudioService? _audio;
    private GlobalHotkeys? _hotkeys;
    private ClipboardService? _clips;
    private ScreenshotLibrary? _shots;
    private RadioService? _radios;
    private NetworkStatusService? _network;
    private BatteryService? _battery;
    private ThemeService? _theme;
    private BrightnessService? _brightness;
    private BluetoothService? _bluetooth;
    private QuickServices? _quick;
    private readonly Panels.AppIndex _appIndex = new();
    private Assistant.AdaAssistant? _ada;
    private GeminiKeyStore? _adaKeys;
    private MemoryFile? _adaMemory;
    private UsageFile? _adaUsage;
    private VoiceprintFile? _adaVoiceprint;
    private ReminderFile? _adaReminders;
    private ReminderScheduler? _reminderScheduler;
    private bool _adaHotkeyRegistered;
    private bool _sessionLocked;
    private readonly List<DateTime> _watchdogStarts = new();
    private TaskbarSafetyStore _safety = null!;
    private SettingsStore _settingsStore = null!;
    private AppSettings _settings = null!;
    private bool _exiting;
    private bool _hostsTray;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
#if DEBUG
        if (await Assistant.AdaProbes.TryRunAsync(e.Args))
        {
            Shutdown();
            return;
        }
#endif
        _safety = new TaskbarSafetyStore(Path.Combine(LocalDir, "taskbar-state.json"));

        switch (StartupMode.Parse(e.Args))
        {
            case StartupMode.Watchdog watchdog:
                await WatchdogRunner.RunAsync(watchdog.ParentProcessId, _safety);
                Shutdown();
                return;

            case StartupMode.RestoreTaskbar:
                RestoreRunner.Run(_safety);
                Shutdown();
                return;

            case StartupMode.Normal normal:
                await StartNormalAsync(normal.Restarting);
                return;
        }
    }

    private async Task StartNormalAsync(bool restarting)
    {
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\CamAda.Main", out var createdNew);
        if (!createdNew && !(restarting && WaitForPreviousInstance(_singleInstance)))
        {
            Shutdown();
            return;
        }

        if (PackageContext.IsPackaged)
        {
            AppRestart.RegisterAfterUpdate(); // Store güncellemesi kapattıktan sonra Windows yeniden açsın
        }

        // Önceki oturum hem ana süreci hem bekçiyi kaybettiyse görev çubuğunu önce geri getir.
        if (WatchdogDecision.ShouldRestore(_safety.Load()))
        {
            RestoreRunner.RestoreTaskbar(_safety);
        }

        StartWatchdog();

        _settingsStore = new SettingsStore(RoamingDir);
        _settings = _settingsStore.Load();
        EnsurePinnedApps();
        _autostart = new AutostartService(PackageContext.IsPackaged, LogCrash);
        _ = _autostart.ApplyAsync(_settings.StartWithWindows);

        _hostsTray = _settings.HideWindowsTaskbar;
        _shell = new ShellHost(_hostsTray);
        _guard = new TaskbarGuard(_shell.Manager.ExplorerHelper, _safety);

        DispatcherUnhandledException += (_, e) => OnCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => OnCrash(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => LogCrash(e.Exception);
        SessionEnding += (_, _) => ExitGracefully();

        // Çalma durumu ada, panel ve plaktan önce hazır olsun (hepsi aynı servisi dinler). Görev çubuğu kurtarma, bekçi ve
        // çökme işleyicileri kurulduktan sonra, görev çubuğu gizlenmeden önce: medya hizmeti takılırsa görev çubuğu açık kalır.
        _media = await NowPlayingService.CreateAsync(Dispatcher);
        if (_exiting)
        {
            return; // beklerken oturum kapandı
        }

        if (_settings.HideWindowsTaskbar)
        {
            _guard.Hide();
        }

        _strip = new StripAppBar(_shell, _settings.StripHeight);
        _strip.Show();

        _dock = new DockViewModel(_shell.Manager, () => _settings.PinnedApps ?? [], Dispatcher);
        _island = new IslandWindow(_strip, _settings, new IslandCommands(
            IsTaskbarHidden: () => _guard.IsHidden,
            SetTaskbarHidden: SetTaskbarHidden,
            Exit: ExitGracefully,
            GetGlassMode: () => _settings.GlassMode,
            SetGlassMode: mode =>
            {
                _settings = _settings with { GlassMode = mode };
                _settingsStore.Save(_settings);
            },
            IsStartWithWindows: () => _autostart!.IsOn,
            SetStartWithWindows: SetStartWithWindows,
            IsRecordHidden: () => _record is null,
            SetRecordHidden: SetRecordHidden,
            IsAssistantEnabled: () => _settings.Assistant.Enabled,
            SetAssistantEnabled: SetAssistantEnabled,
            OpenAssistantPanel: OpenAssistantPanel), _dock, _media);
        _island.Bar.TrayClicked += (_, _) => OpenTrayPanel();
        _island.Bar.StartClicked += (_, _) => OpenStartPanel();
        _island.Bar.SearchClicked += (_, _) => OpenStartPanel();
        _island.LocationChanged += (_, _) => UpdateTrayHostSizeData(); // ekran ayarı değişince ada yeniden yerleşir
        _island.Show();

        // Beğen ve sıradaki şarkılar (Spotify Web API): bağlı değilken istek atılmaz, kart ve panel bu denetimleri gizler.
        _spotify = new SpotifyClient(() => _settings.SpotifyClientId, new SpotifyTokenStore(LocalDir), TimeProvider.System);
        _spotifyExtras = new SpotifyExtras(_spotify, _media!, Dispatcher);
        _island.MusicExtras = _spotifyExtras;
        // "İzni yenile" yalnız Ada açıkken: eski izin beğen ve sıradakiler için yeterli.
        _island.MusicFooter = () =>
            Core.Spotify.SpotifyFooter.For(_spotify.IsConnected, !_spotify.NeedsReconnect, _settings.Assistant.Enabled) == Core.Spotify.SpotifyFooterKind.None
                ? null
                : new SpotifyConnectView(_spotify, SaveSpotifyClientId, () => _settings.Assistant.Enabled);

        if (_settings.Record.Enabled)
        {
            ShowRecord();
        }

        _hotkeys = new GlobalHotkeys();
        StartAudio();
        StartClipboard();

        _weather = new WeatherService(() => _settings.Weather, SaveWeatherLocation, Dispatcher);
        _weather.Changed += (_, _) => _island?.ShowWeather(_weather.Current);
        _weather.Start();
        _ = Dispatcher.BeginInvoke(UpdateTrayHostSizeData, DispatcherPriority.Loaded);

        _ = _appIndex.GetItemsAsync(); // ilk aramada beklememek için arka planda hazırla

        // En sonda: WinRT erişim istekleri yavaşsa bile hava durumu, tepsi ve arama beklemesin (son inceleme).
        await StartQuickSettingsAsync();
        StartAssistant();
    }

    /// <summary>
    /// Sesli asistan Ada (Plan 5): servisler hazır olunca kurulur. Kapalıysa (varsayılan) ne dinler ne kısayol alır; açıksa
    /// "Hey Ada" dinler ve Win+Alt+A ile tetiklenir. Kilitte ve uykuda oturum kapanır, uyandırma durur.
    /// </summary>
    private void StartAssistant()
    {
        if (_exiting || _island is null)
        {
            return;
        }

        var keys = _adaKeys = new GeminiKeyStore(LocalDir);
        keys.ImportLegacy(Path.Combine(LocalDir, "gemini-anahtar.txt"));
        var memory = _adaMemory = new MemoryFile(LocalDir, TimeProvider.System);
        _adaUsage = new UsageFile(LocalDir);
        var tools = new AdaToolContext(_audio, _brightness, _radios, _media, _spotify, _appIndex, _quick?.Launcher ?? CreateQuickLauncher(),
            PowerActions.Create(), AdaOpenWindows, memory, new CurrentInfoClient(keys.Load, () => _settings.Assistant.LookupModel),
            () => _settings.Assistant, EndSessionAfterSpeech: () => { }, LookupUsed: _ => { })
        {
            Reminders = _adaReminders = new ReminderFile(LocalDir, TimeProvider.System),
            Weather = new Weather.WeatherLookup(() => _settings.Weather),
        };
        _adaVoiceprint = new VoiceprintFile(LocalDir);
        _ada = new AdaAssistant(Dispatcher, new AdaDependencies(() => _settings.Assistant, keys, tools, memory, _adaUsage,
            Path.Combine(AppContext.BaseDirectory, "Assets", "WakeWord"), LogCrash, _audio) { Voiceprint = _adaVoiceprint });
        _ada.Notice += (_, text) => _island?.Flash(new FlashContent("", null, text));
        _ada.GuestModeChanged += (_, on) => _island?.ShowAdaGuest(on);

        // Hatırlatmalar (Plan 9): Ada kapalıyken de çalışır (yalnız ton ve yazı); CamAda kapalıyken dolanlar açılışta söylenir.
        _reminderScheduler = new ReminderScheduler(_adaReminders, Dispatcher);
        _reminderScheduler.Due += OnRemindersDue;
        _reminderScheduler.Tick += ShowNextTimer;
        _reminderScheduler.Start();
        _island.ShowAdaGuest(_ada.IsGuestMode);
        _ada.StateChanged += (_, state) =>
        {
            Debug.WriteLine($"Ada: {state}");
            _island?.ShowAda(state);
            _island?.Bar.ShowAssistantActive(_ada?.IsSessionActive == true);
            if (state is AdaState.Failed { Error: AssistantError.InvalidKey } && _strip?.IsFullScreenActive != true)
            {
                OpenAssistantPanel(); // tasarım: "Gemini anahtarı geçersiz" ve ayarlar paneli (tam ekranda oyunu bölmez)
            }
        };
        _island.Bar.AssistantClicked += (_, _) => OnAssistantClicked();
        _island.AdaRowClicked += (_, _) => _ada?.Trigger();
        _strip!.FullScreenChanged += (_, fullScreen) => _ada?.SetFullScreen(fullScreen);
        _ada.SetFullScreen(_strip.IsFullScreenActive);
        Microsoft.Win32.SystemEvents.SessionSwitch += OnSessionSwitchForAda;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnPowerModeForAda;
        if (_settings.Assistant.Enabled)
        {
            EnsureAssistantHotkey();
            _ada.Start();
        }
    }

    /// <summary>Ada düğmesi: asistan açık ve anahtar varsa oturumu açar/kapatır; değilse Ada ayarlarını açar.</summary>
    private void OnAssistantClicked()
    {
        if (_settings.Assistant.Enabled && _adaKeys?.Load() is not null)
        {
            _ada?.Trigger();
            return;
        }

        OpenAssistantPanel();
    }

    /// <summary>Menüdeki "Sesli asistan" ve paneldeki anahtar: anahtar yoksa açmak yerine Ada ayarlarını açar.</summary>
    private void SetAssistantEnabled(bool on)
    {
        if (on && _adaKeys?.Load() is null)
        {
            OpenAssistantPanel();
            return;
        }

        SaveAssistantSettings(_settings.Assistant with { Enabled = on });
        if (on)
        {
            EnsureAssistantHotkey();
            _ada?.Start();
        }
        else
        {
            _ada?.Stop();
        }
    }

    private void SaveAssistantSettings(AssistantSettings assistant)
    {
        _settings = _settings with { Assistant = assistant };
        _settingsStore.Save(_settings);
        _ada?.Refresh();
    }

    private void OpenAssistantPanel()
    {
        if (_island is null || _adaKeys is null || _adaMemory is null || _adaUsage is null || _adaVoiceprint is null
            || _island.Panels.IsOpenWith("ada"))
        {
            return;
        }

        var services = new Panels.AdaPanelServices(_adaKeys, _adaMemory, _adaUsage, () => _settings.Assistant, SaveAssistantSettings,
            () => _audio?.Inputs() ?? [], ValidateGeminiKeyAsync, SetAssistantEnabled, () => _ada?.Refresh(),
            _adaVoiceprint, () => _ada?.ReserveMic());
        _island.Panels.Show("ada", new Panels.AdaPanel(services), Panels.AdaPanel.PanelWidth);
        _island.ExpandForKeyboard(); // menüden açılınca da panel adaya bağlı kalsın, anahtar kutusuna yazılabilsin
    }

    /// <summary>Anahtarı ses göndermeden bir Live kurulumuyla dener (ücret doğmaz); sorun yoksa None.</summary>
    private async Task<AssistantError> ValidateGeminiKeyAsync(string key)
    {
        var assistant = _settings.Assistant;
        var setup = LiveSetup.Build(new LiveSetupOptions(assistant.Model, assistant.Voice, "Anahtar denetimi.", AdaTools.Declarations()));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
        try
        {
            await using var client = await GeminiLiveClient.ConnectAsync(key, setup, timeout.Token);
            await client.CloseAsync();
            return AssistantError.None;
        }
        catch (AssistantException ex)
        {
            return ex.Error;
        }
        catch (OperationCanceledException)
        {
            return AssistantError.Network;
        }
    }

    /// <summary>Ada kısayolu (ayardan; varsayılan Win+Alt+A) bir kez alınır; alınamazsa adada bir kez söylenir.</summary>
    private void EnsureAssistantHotkey()
    {
        if (_adaHotkeyRegistered || _hotkeys is null)
        {
            return;
        }

        var text = _settings.Assistant.Hotkey;
        if (Hotkey.TryParse(text) is not { } hotkey)
        {
            return;
        }

        _adaHotkeyRegistered = _hotkeys.Register(hotkey, () =>
        {
            if (_settings.Assistant.Enabled)
            {
                _ada?.Trigger();
            }
        });
        if (!_adaHotkeyRegistered)
        {
            _ = Dispatcher.BeginInvoke(() => _island?.Flash(new FlashContent("", null, $"{text} kullanılamıyor")),
                DispatcherPriority.ApplicationIdle);
        }
    }

    /// <summary>ManagedShell görev listesindeki pencereler; uygulama adı başlıktan değil AUMID'den ya da exe açıklamasından.</summary>
    private IReadOnlyList<OpenWindow> AdaOpenWindows()
    {
        var apps = _appIndex.GetItemsAsync() is { IsCompletedSuccessfully: true } built ? built.Result : [];
        return _shell?.Manager.Tasks?.GroupedWindows?.Cast<ManagedShell.WindowsTasks.ApplicationWindow>()
            .Select(w => new OpenWindow(w.Handle, OpenWindowNames.AppName(w.AppUserModelID, w.WinFileName, apps),
                Path.GetFileNameWithoutExtension(w.WinFileName ?? ""), w.Title ?? ""))
            .ToList() ?? [];
    }

    private void OnSessionSwitchForAda(object? sender, Microsoft.Win32.SessionSwitchEventArgs e) => Dispatcher.BeginInvoke(() =>
    {
        if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionLock)
        {
            _sessionLocked = true;
            _ada?.Stop();
        }
        else if (e.Reason == Microsoft.Win32.SessionSwitchReason.SessionUnlock)
        {
            _sessionLocked = false;
            ShowUnseenReminder(); // kilitliyken dolan hatırlatmanın yazısı
            if (_settings.Assistant.Enabled)
            {
                _ada?.Start();
            }
        }
    });

    private static readonly TimeSpan ReminderTextHold = TimeSpan.FromSeconds(8);
    private static readonly TimeSpan ReminderRetryAfter = TimeSpan.FromSeconds(10);

    /// <summary>Gösterilemeyen hatırlatma yazısı (kilit, ada genişken, panel açıkken): kilit açılınca ya da ada boşalınca gösterilir.</summary>
    private string? _unseenReminder;

    /// <summary>
    /// Hatırlatma zamanı geldi: adada 8 sn yazı, ton ve Ada'nın sesi (Ada'nın sesi yoksa yalnız ton ve yazı). Söylenemezse (uykudan
    /// yeni uyanıldı, internet ya da ses cihazı henüz gelmedi) 10 sn sonra bir kez daha denenir; sessizce kaybolmaz (son inceleme).
    /// </summary>
    private async void OnRemindersDue(IReadOnlyList<Reminder> due)
    {
        var sentence = ReminderText.Announcement(due, DateTimeOffset.Now);
        ShowReminderText(sentence);
        if (_ada is not { } ada)
        {
            return;
        }

        try
        {
            if (await ada.AnnounceAsync(sentence) == AnnounceResult.Failed && !_exiting)
            {
                await Task.Delay(ReminderRetryAfter);
                if (!_exiting)
                {
                    ShowReminderText(sentence);
                    await ada.AnnounceAsync(sentence);
                }
            }
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            LogCrash(ex); // duyuru hatası uygulamayı düşürmesin
        }
    }

    private void ShowReminderText(string sentence)
    {
        var shown = !_sessionLocked && _island?.Flash(new Island.FlashContent("", null, sentence), ReminderTextHold) == true;
        _unseenReminder = shown ? null : _unseenReminder is null ? sentence : $"{_unseenReminder} {sentence}";
    }

    /// <summary>Bekleyen hatırlatma yazısını dener (her denetimde ve kilit açılınca).</summary>
    private void ShowUnseenReminder()
    {
        if (_unseenReminder is { } text && !_sessionLocked && _island?.Flash(new Island.FlashContent("", null, text), ReminderTextHold) == true)
        {
            _unseenReminder = null;
        }
    }

    /// <summary>Küçük adada 60 dk'dan az kalan en yakın hatırlatmanın geri sayımı.</summary>
    private void ShowNextTimer()
    {
        ShowUnseenReminder();
        var now = DateTimeOffset.Now;
        if (_adaReminders?.Book.Pending is not [var next, ..] || ReminderText.Countdown(next.At, now) is not { } countdown)
        {
            _island?.ShowTimer(null, null);
            return;
        }

        var left = next.At - now;
        _island?.ShowTimer(countdown, $"Zamanlayıcı: {next.Text}, {(int)left.TotalMinutes} dakika {left.Seconds} saniye");
    }

    private void OnPowerModeForAda(object? sender, Microsoft.Win32.PowerModeChangedEventArgs e) => Dispatcher.BeginInvoke(async () =>
    {
        if (e.Mode == Microsoft.Win32.PowerModes.Suspend)
        {
            _ada?.Stop();
        }
        else if (e.Mode == Microsoft.Win32.PowerModes.Resume)
        {
            _reminderScheduler?.CheckNow(); // uykudayken dolanlar "kaçırılan" olarak hemen söylenir
            // Uyandırma yeniden kurulur (mikrofon uykuda kopmuş olabilir), ama kilit ekranında asla: biri "Hey Ada, benim
            // hakkımda ne biliyorsun" diyemesin. Windows kilit olayını uyanıştan hemen sonra da gönderebildiği için beklenir.
            await Task.Delay(TimeSpan.FromSeconds(3));
            if (!_sessionLocked && !_exiting && _settings.Assistant.Enabled)
            {
                _ada?.Start();
            }
        }
    });

    /// <summary>
    /// Ses servisi (spec §7.4 "Ses ve mikrofon"): ses düğmesi, Ses paneli. Core Audio kurulamazsa (ör. ses hizmeti
    /// kapalı) ada sessiz simgeyle çalışmaya devam eder; ses düğmesine basınca yeniden denenir.
    /// </summary>
    private void StartAudio()
    {
        _island!.Bar.SoundClicked += (_, _) => OnSoundClicked();
        TryStartAudio();
    }

    private void OnSoundClicked()
    {
        if (!TryStartAudio())
        {
            _island?.Flash(new FlashContent(SpeakerGlyph.For(SpeakerLevel.Muted), null, "Ses hizmetine ulaşılamadı"));
            return;
        }

        var audio = _audio!;
        _island!.Panels.Toggle("sound", () => new Panels.SoundPanel(audio), Panels.SoundPanel.PanelWidth);
    }

    /// <summary>Ses servisini kurar (kuruluysa dokunmaz); kurulamazsa false.</summary>
    private bool TryStartAudio()
    {
        if (_audio is not null)
        {
            return true;
        }

        AudioService audio;
        try
        {
            audio = new AudioService(Dispatcher);
        }
        catch (Exception ex) when (CoreAudioErrors.IsRecoverable(ex))
        {
            LogCrash(ex);
            _island?.Bar.ShowSound(SpeakerLevel.Muted, micMuted: false);
            return false;
        }

        _audio = audio;
        audio.MasterChanged += (_, change) =>
        {
            UpdateSoundButton();
            if (!change.FromCamAda) // CamAda'nın kaydırıcısıyla değişince panel zaten gösteriyor (Review Focus 3)
            {
                _island?.Flash(new FlashContent(
                    SpeakerGlyph.For(SpeakerLevels.For(change.Volume, change.Muted)),
                    change.Volume,
                    change.Muted ? "Sessiz" : $"%{SpeakerLevels.Percent(change.Volume)}"));
            }
        };
        audio.DevicesChanged += (_, _) => UpdateSoundButton();
        audio.MicChanged += (_, _) => UpdateSoundButton();
        UpdateSoundButton();
        RegisterMicHotkey(audio);
        return true;
    }

    /// <summary>
    /// Pano geçmişi (tasarım "Geçmiş"): pano düğmesi ve Pano paneli. Dinleyici kurulamazsa ada panosuz çalışır.
    /// </summary>
    private void StartClipboard()
    {
        try
        {
            _clips = new ClipboardService(Dispatcher, Path.Combine(RoamingDir, "clipboard"), LogCrash);
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            LogCrash(ex);
            return;
        }

        _shots = new ScreenshotLibrary(Dispatcher, _clips, ScreenshotFolder(), LogCrash);
        _shots.Arrived += (_, file) => ShowScreenshotPreview(file);
        _shots.ClipboardOnlyShot += (_, item) => ShowScreenshotPreview(item);
        _island!.Bar.ClipboardClicked += (_, _) =>
            _island.Panels.Toggle("clipboard", () => CreateClipboardPanel(pasteOnChoose: false), ClipboardPanel.PanelWidth);
        RegisterClipboardHotkey();
    }

    private ClipboardPanel CreateClipboardPanel(bool pasteOnChoose) => new(_clips!, _shots, pasteOnChoose);

    /// <summary>
    /// Hızlı ayarlar ve pil (Plan 4.3): adadaki Wi‑Fi ve pil simgeleri, küçük adada düşük pil. Telsiz erişimi kurulamazsa
    /// ada telsizsiz çalışır (Wi‑Fi simgesi yalnız bağlantıyı gösterir).
    /// </summary>
    private async Task StartQuickSettingsAsync()
    {
        try
        {
            _radios = await RadioService.CreateAsync(Dispatcher);
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            LogCrash(ex);
        }

        _network = new NetworkStatusService(Dispatcher, _radios);
        _network.Changed += (_, _) => UpdateNetworkButton();
        _battery = new BatteryService(Dispatcher);
        _battery.Changed += (_, _) => UpdateBatteryButton();
        _island!.Expanding += (_, _) => _network.Refresh();
        UpdateNetworkButton();
        UpdateBatteryButton();

        _theme = new ThemeService(Dispatcher);
        if (!IsAbsentForTest("brightness"))
        {
            _brightness = new BrightnessService(Dispatcher, TimeProvider.System);
            _brightness.InternalChanged += (_, change) =>
            {
                if (!change.FromCamAda) // CamAda'nın kaydırıcısıyla değişince panel zaten gösteriyor
                {
                    _island?.Flash(new FlashContent(QuickGlyphs.Brightness, change.Level / 100.0, $"%{change.Level}"));
                }
            };
        }

        _quick = new QuickServices
        {
            Network = _network,
            Battery = _battery,
            Theme = _theme,
            Launcher = CreateQuickLauncher(),
            Radios = IsAbsentForTest("radios") ? null : _radios,
            Brightness = _brightness,
            Wifi = await CreateWifiAsync(_radios?.HasWifi ?? false),
            Bluetooth = _bluetooth = IsAbsentForTest("bluetooth") ? null : new BluetoothService(Dispatcher),
        };
        _island.Bar.WifiClicked += (_, _) => OpenQuickSettings();
        _island.Bar.BatteryClicked += (_, _) => OpenQuickSettings();
    }

    /// <summary>
    /// Wi‑Fi listesi; adaptör yoksa ya da kurulamazsa null (kutucukta liste oku görünmez). Paketliyken erişim izni
    /// açılışta değil, liste ilk açıldığında istenir.
    /// </summary>
    private static async Task<WifiService?> CreateWifiAsync(bool hasWifiRadio)
    {
        if (IsAbsentForTest("wifi"))
        {
            return null;
        }

        try
        {
            return WifiStartup.Decide(PackageContext.IsPackaged, hasWifiRadio) switch
            {
                WifiStartupMode.RequestNow => await WifiService.CreateAsync(),
                WifiStartupMode.Deferred => WifiService.CreateDeferred(),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        {
            LogCrash(ex);
            return null;
        }
    }

    /// <summary>Wi‑Fi ve pil simgesi aynı paneli açar; ikisinden birine yeniden basmak kapatır.</summary>
    private void OpenQuickSettings() =>
        _island?.Panels.Toggle("quick", () => new QuickSettingsPanel(_quick!), QuickSettingsPanel.PanelWidth);

    private static IQuickLauncher CreateQuickLauncher()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("CAMADA_FAKE_LAUNCHER") is { Length: > 0 } log)
        {
            return new RecordingQuickLauncher(log);
        }
#endif
        return new SystemQuickLauncher();
    }

    /// <summary>Yalnız Debug denemesi: <c>CAMADA_QS_ABSENT=radios,brightness,wifi,bluetooth</c> servisleri yok sayar.</summary>
    private static bool IsAbsentForTest(string service)
    {
#if DEBUG
        return Environment.GetEnvironmentVariable("CAMADA_QS_ABSENT")?.Split(',').Contains(service) == true;
#else
        return false;
#endif
    }

    private void UpdateNetworkButton()
    {
        if (_network is null || _island is null)
        {
            return;
        }

        var snapshot = _network.Current;
        _island.Bar.ShowNetwork(NetworkIcons.For(snapshot), NetworkIcons.AccessibleName(snapshot));
    }

    private void UpdateBatteryButton()
    {
        if (_battery is null || _island is null)
        {
            return;
        }

        var snapshot = _battery.Current;
        _island.Bar.ShowBattery(_battery.Present ? BatteryText.Icon(snapshot) : null, BatteryText.AccessibleName(snapshot));
        _island.ShowLowBattery(BatteryText.IsLow(snapshot) ? BatteryText.CompactText(snapshot) : null);
    }

    /// <summary>Yeni ekran görüntüsü dosyası: adada önizleme; Kopyala dosyayı panoya alır, Düzenle aracı açar.</summary>
    private void ShowScreenshotPreview(ScreenshotFile file)
    {
        if (_island is null || _clips is null || _shots?.Thumbnail(file) is not { } thumbnail)
        {
            return;
        }

        var clips = _clips;
        _island.ShowScreenshot(thumbnail,
            () => clips.PutAsync(new ScreenshotChoice(file.Path)),
            () => ScreenshotLibrary.OpenForEdit(file.Path));
    }

    /// <summary>Yalnız panoya giden ekran görüntüsü: Düzenle önce geçici bir PNG'ye yazar.</summary>
    private void ShowScreenshotPreview(ClipItem item)
    {
        if (_island is null || _clips is null || _clips.ThumbnailFor(item) is not { } thumbnail || item.Payload.Png is not { } png)
        {
            return;
        }

        var clips = _clips;
        _island.ShowScreenshot(thumbnail,
            () => clips.PutAsync(new HistoryChoice(item.Id)),
            () =>
            {
                try
                {
                    var folder = Path.Combine(Path.GetTempPath(), "CamAda");
                    Directory.CreateDirectory(folder);
                    DeleteOldTempShots(folder);
                    var path = Path.Combine(folder, $"shot-{DateTime.Now:yyyyMMdd-HHmmss}.png");
                    File.WriteAllBytes(path, png);
                    ScreenshotLibrary.OpenForEdit(path);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    LogCrash(ex);
                }
            });
    }

    /// <summary>Düzenlemeye açılmış geçici ekran görüntülerinden bir günden eskileri siler (düzenleyici çoktan kapanmıştır).</summary>
    private static void DeleteOldTempShots(string folder)
    {
        var cutoff = DateTime.UtcNow - TimeSpan.FromDays(1);
        foreach (var file in new DirectoryInfo(folder).EnumerateFiles("shot-*.png"))
        {
            try
            {
                if (file.LastWriteTimeUtc < cutoff)
                {
                    file.Delete();
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                // hâlâ açık olabilir; sonraki sefere
            }
        }
    }

    /// <summary>Ekran görüntüsü klasörü; Debug derlemesinde CAMADA_SCREENSHOTS ortam değişkeni önceliklidir (deneme).</summary>
    private static string? ScreenshotFolder()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("CAMADA_SCREENSHOTS") is { Length: > 0 } overridden)
        {
            return overridden;
        }
#endif
        return ScreenshotLibrary.KnownFolder();
    }

    /// <summary>
    /// Win+Alt+V (ayardan değişir): Pano panelini her yerden açar; seçilen öğe kısayol anında öndeki pencereye yapıştırılır
    /// (tasarım "Win+Alt+V ve yapıştırma"). Kısayolu başka bir uygulama almışsa adada bir kez "… kullanılamıyor" görünür.
    /// </summary>
    private void RegisterClipboardHotkey()
    {
        var text = _settings.Hotkeys.ClipboardOpen;
        if (Hotkey.TryParse(text) is not { } hotkey || _hotkeys is null)
        {
            return;
        }

        if (!_hotkeys.Register(hotkey, OnClipboardHotkey))
        {
            _ = Dispatcher.BeginInvoke(() => _island?.Flash(new FlashContent("\uE77F", null, $"{text} kullanılamıyor")),
                DispatcherPriority.ApplicationIdle);
        }
    }

    private void OnClipboardHotkey()
    {
        if (_island is null || _clips is null)
        {
            return;
        }

        if (_island.Panels.IsOpenWith("clipboard"))
        {
            _island.Panels.Close();
            return;
        }

        // Öndeki pencere yoksa yapıştırılacak yer yok: düğmeyle açılmış gibi yalnız kopyalanır ve panel açık kalır.
        var target = PasteBack.CaptureForeground();
        var pasteOnChoose = target != 0
                            && PasteRule.Decide(openedByHotkey: true, PasteBack.Describe(target, Environment.ProcessId))
                            != PasteOutcome.CopyOnly;
        var panel = CreateClipboardPanel(pasteOnChoose);
        var chosen = false;
        if (pasteOnChoose)
        {
            panel.Chosen += (_, choice) =>
            {
                chosen = true;
                _ = PasteChoiceAsync(choice, target);
            };

            // Esc ya da kısayolla seçimsiz kapanınca odağı önceki pencereye geri ver; kullanıcı başka bir pencereye
            // tıkladıysa ona dokunma.
            panel.Unloaded += (_, _) =>
            {
                var foreground = Native.GetForegroundWindow();
                var otherPanelOpened = _island.Panels.IsOpen && !_island.Panels.IsOpenWith("clipboard");
                if (!chosen && !otherPanelOpened
                    && (foreground == 0 || Native.ProcessIdOf(foreground) == Environment.ProcessId))
                {
                    Native.SetForegroundWindow(target);
                }
            };
        }

        _island.Panels.Show("clipboard", panel, ClipboardPanel.PanelWidth);
        _island.ExpandForKeyboard();
    }

    private async Task PasteChoiceAsync(ClipChoice choice, nint target)
    {
        var copied = await _clips!.PutAsync(choice);
        _island?.Panels.Close();
        if (!copied)
        {
            _island?.Flash(new FlashContent("\uE8C8", null, "Kopyalanamadı — pano meşgul"));
            return;
        }

        var outcome = PasteRule.Decide(openedByHotkey: true, PasteBack.Describe(target, Environment.ProcessId));
        if (outcome == PasteOutcome.Paste && await PasteBack.PasteIntoAsync(target))
        {
            return;
        }

        _island?.Flash(new FlashContent("\uE8C8", null, "Kopyalandı — Ctrl+V ile yapıştır"));
    }

    /// <summary>
    /// Ctrl+Shift+M (ayardan değişir): mikrofonu her yerden kapatıp açar ve adada kısaca gösterir. Kısayolu başka bir
    /// uygulama almışsa adada bir kez "… kullanılamıyor" görünür (Review Focus 4).
    /// </summary>
    private void RegisterMicHotkey(AudioService audio)
    {
        var text = _settings.Hotkeys.MicToggle;
        if (Hotkey.TryParse(text) is not { } hotkey)
        {
            return;
        }

        var registered = _hotkeys!.Register(hotkey, () =>
        {
            audio.ToggleMic();
            _island?.Flash(audio.Mic switch
            {
                MicState.Off => new FlashContent(SpeakerGlyph.MicOff, null, "Mikrofon kapalı"),
                MicState.On => new FlashContent(SpeakerGlyph.MicOn, null, "Mikrofon açık"),
                _ => new FlashContent(SpeakerGlyph.MicOff, null, "Mikrofon bulunamadı"),
            });
        });

        if (!registered)
        {
            _ = Dispatcher.BeginInvoke(() => _island?.Flash(new FlashContent(SpeakerGlyph.MicOn, null, $"{text} kullanılamıyor")),
                DispatcherPriority.ApplicationIdle);
        }
    }

    private void UpdateSoundButton()
    {
        if (_audio is null || _island is null)
        {
            return;
        }

        var micOff = _audio.Mic == MicState.Off;
        _island.Bar.ShowSound(SpeakerLevels.For(_audio.Volume, _audio.Muted || !_audio.HasOutput), micOff);
        _island.ShowMicOff(micOff);
    }

    /// <summary>Başlat ve Ara aynı paneli açar (yazmaya hazır arama kutusuyla).</summary>
    private void OpenStartPanel()
    {
        if (_island is null || _dock is null)
        {
            return;
        }

        var tiles = (_settings.PinnedApps ?? [])
            .Where(p => File.Exists(p.LaunchPath))
            .Select(p => new Panels.StartTile(
                p.Name,
                _dock.IconFor(new DockEntry("pin:" + p.Id, p, [])),
                () => ManagedShell.Common.Helpers.ShellHelper.StartProcess(p.LaunchPath, p.Arguments)))
            .ToList();
        _island.Panels.Toggle("start", () => new Panels.StartPanel(_appIndex, tiles), Panels.StartPanel.PanelWidth);
    }

    /// <summary>
    /// "Arka planda çalışanlar" panelini açar/kapatır. Önce ManagedShell'e tepsinin üst kenarda, adanın yerinde olduğunu
    /// bildirir; uygulamaların açılır pencereleri böylece üstte belirir.
    /// </summary>
    private void OpenTrayPanel()
    {
        if (_island is null || _shell?.Manager.NotificationArea is not { IsFailed: false } area)
        {
            return;
        }

        UpdateTrayHostSizeData(); // genişlemiş hapın yeri

        // Sağ tık menüsü odağı alınca panel kapanmasın (imleç paneldeyken).
        _island.Panels.Toggle("tray", () => new Panels.TrayPanel(area), Panels.TrayPanel.PanelWidth,
            keepOpenWhilePointerInside: true);
    }

    /// <summary>
    /// Tepsiyi barındırırken uygulamalara "görev çubuğu"nun yerini (ABM_GETTASKBARPOS) adanın hapı olarak bildirir;
    /// açılır pencerelerini buna göre yerleştirirler. Açılışta ve ada yer değiştirince yapılır, yoksa panel ilk açılana
    /// kadar ManagedShell'in varsayılanı (üstte tam genişlikte 23 px'lik çubuk) bildirilirdi.
    /// </summary>
    private void UpdateTrayHostSizeData()
    {
        if (!_hostsTray || _island is null || _shell?.Manager.NotificationArea is not { IsFailed: false } area
            || PresentationSource.FromVisual(_island.Pill) is null)
        {
            return;
        }

        var pill = _island.Pill;
        var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(pill);
        var topLeft = pill.PointToScreen(new Point(0, 0));
        area.SetTrayHostSizeData(new ManagedShell.WindowsTray.TrayHostSizeData
        {
            edge = ManagedShell.Interop.NativeMethods.ABEdge.ABE_TOP,
            rc = new ManagedShell.Interop.NativeMethods.Rect
            {
                Left = (int)topLeft.X,
                Top = (int)topLeft.Y,
                Right = (int)(topLeft.X + pill.ActualWidth * dpi.DpiScaleX),
                Bottom = (int)(topLeft.Y + pill.ActualHeight * dpi.DpiScaleY),
            },
        });
    }

    /// <summary>İlk açılışta (ayarda sabitlenmiş uygulama yoksa) Chrome, Dosya Gezgini, Steam ve Discord'u arar ve kaydeder.</summary>
    private void EnsurePinnedApps()
    {
        if (_settings.PinnedApps is not null)
        {
            return;
        }

        string? steamExe = null;
        using (var steamKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam"))
        {
            if (steamKey?.GetValue("SteamExe") is string path && !string.IsNullOrWhiteSpace(path))
            {
                steamExe = path.Replace('/', '\\');
            }
        }

        var pinned = PinnedAppDefaults.Resolve(Environment.ExpandEnvironmentVariables, File.Exists, steamExe);
        _settings = _settings with { PinnedApps = pinned };
        _settingsStore.Save(_settings);
    }

    /// <summary>Müzik panelinde yapıştırılan Spotify Client ID'sini ayara yazar.</summary>
    private void SaveSpotifyClientId(string clientId)
    {
        _settings = _settings with { SpotifyClientId = clientId };
        _settingsStore.Save(_settings);
    }

    /// <summary>Hava durumu şehrinin bulunan koordinatlarını ayara yazar (bir kez; şehir değişince yeniden aranır).</summary>
    private void SaveWeatherLocation(WeatherLocation location)
    {
        _settings = _settings with { Weather = location };
        _settingsStore.Save(_settings);
    }

    /// <summary>Adanın menüsünden: ayarı kaydeder; gizlerken plak kapanır (hiç iş yapmaz), gösterirken yeniden kurulur.</summary>
    private void SetRecordHidden(bool hidden)
    {
        _settings = _settings with { Record = _settings.Record with { Enabled = !hidden } };
        _settingsStore.Save(_settings);

        if (hidden)
        {
            CloseRecord();
        }
        else
        {
            ShowRecord();
        }
    }

    /// <summary>Plak çaları kurar ve gösterir (zaten açıksa dokunmaz). Açılıştaki sırayla: önce göster, sonra Spotify ekleri.</summary>
    private void ShowRecord()
    {
        if (_record is not null)
        {
            return;
        }

        var record = new RecordWindow(_media!, () => _settings.Record);
        // Alt+F4 gibi dışarıdan kapanışta da alan boşalır: menü işareti gerçeği gösterir ve "göster" plağı geri getirir.
        record.Closed += (_, _) =>
        {
            if (ReferenceEquals(_record, record))
            {
                _record = null;
            }
        };
        _record = record;
        record.Show();
        record.Extras = _spotifyExtras;
    }

    /// <summary>Plak çaları kapatır; pencere medya, ekran ve örtülme olaylarını kapanırken bırakır.</summary>
    private void CloseRecord()
    {
        _record?.Close();
        _record = null;
    }

    private async void SetStartWithWindows(bool enabled)
    {
        _settings = _settings with { StartWithWindows = enabled };
        _settingsStore.Save(_settings);
        var action = await _autostart!.ApplyAsync(enabled);
        if (action is StartupTaskAction.BlockedByUser or StartupTaskAction.BlockedByPolicy)
        {
            // Kullanıcı Windows'ta kapatmış: uygulama zorla açamaz, açabileceği yeri gösterir.
            try
            {
                Process.Start(new ProcessStartInfo("ms-settings:startupapps") { UseShellExecute = true })?.Dispose();
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                LogCrash(ex);
            }
        }
    }

    private void SetTaskbarHidden(bool hidden)
    {
        _settings = _settings with { HideWindowsTaskbar = hidden };
        _settingsStore.Save(_settings);

        if (hidden)
        {
            _guard!.Hide();
        }
        else
        {
            _guard!.ShowAndMarkClean();
        }

        // Tepsiyi barındırma yalnız açılışta seçilebilir: görev çubuğu görünürken simgeler Explorer'ın tepsisinde olmalı.
        if (hidden != _hostsTray)
        {
            Restart();
        }
    }

    /// <summary>Önceki örnek kapanırken tekil örnek kilidini devralır (yeniden başlatma).</summary>
    private static bool WaitForPreviousInstance(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(TimeSpan.FromSeconds(10));
        }
        catch (AbandonedMutexException)
        {
            return true; // önceki örnek kilidi bırakmadan çıktı: kilit artık bizde
        }
    }

    /// <summary>Yeni örneği başlatır (önceki kapanana kadar bekler) ve bu örneği temiz kapatır.</summary>
    private void Restart()
    {
        if (Environment.ProcessPath is not { } exe)
        {
            return;
        }

        try
        {
            DetachedProcess.Start(exe, StartupMode.RestartingArgument);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Debug.WriteLine($"Yeniden başlatılamadı: {ex}");
            return;
        }

        ExitGracefully();
    }

    /// <summary>
    /// Bekçiyi ana sürecin ağacı ve iş nesnesi dışında başlatır; bekçi tek başına ölürse yeniden başlatır.
    /// </summary>
    private void StartWatchdog()
    {
        var exe = Environment.ProcessPath;
        if (exe is null || _exiting)
        {
            return;
        }

        // Bekçi sürekli hemen çöküyorsa sonsuz döngüye girme: dakikada en fazla 5 başlatma.
        var now = DateTime.UtcNow;
        _watchdogStarts.RemoveAll(t => now - t > TimeSpan.FromMinutes(1));
        if (_watchdogStarts.Count >= 5)
        {
            Debug.WriteLine("Bekçi bir dakikada 5 kez kapandı; yeniden başlatma durduruldu.");
            return;
        }

        _watchdogStarts.Add(now);

        try
        {
            var pid = DetachedProcess.Start(exe, $"--watchdog {Environment.ProcessId}");
            _watchdog?.Dispose();
            _watchdog = Process.GetProcessById(pid);
            _watchdog.EnableRaisingEvents = true;
            _watchdog.Exited += (_, _) => Dispatcher.BeginInvoke(StartWatchdog);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            Debug.WriteLine($"Bekçi başlatılamadı: {ex}");
        }
    }

    /// <summary>Görev çubuğunu geri getirir, servisleri kapatır ve uygulamadan çıkar.</summary>
    public void ExitGracefully()
    {
        if (_exiting)
        {
            return;
        }

        _exiting = true;
        TryShowTaskbar();
        Microsoft.Win32.SystemEvents.SessionSwitch -= OnSessionSwitchForAda;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnPowerModeForAda;
        _reminderScheduler?.Dispose();
        _ada?.Dispose();
        _island?.Close();
        CloseRecord();
        _hotkeys?.Dispose();
        _shots?.Dispose();
        _clips?.Dispose();
        _network?.Dispose();
        _battery?.Dispose();
        _radios?.Dispose();
        _theme?.Dispose();
        _brightness?.Dispose();
        _bluetooth?.Dispose();
        _audio?.Dispose();
        _weather?.Dispose();
        _media?.Dispose();
        _dock?.Dispose();
        _shell?.Manager.AppBarManager.SignalGracefulShutdown(); // AppBar'ın kapanmasına izin ver
        _strip?.Close();
        _shell?.Shutdown();
        Shutdown();
    }

    /// <summary>Beklenmeyen hata: önce günlüğe yaz, sonra görev çubuğunu geri getir (bekçi zaten yedek).</summary>
    private void OnCrash(Exception? exception)
    {
        if (exception is not null)
        {
            LogCrash(exception);
        }

        TryShowTaskbar();
    }

    private static void LogCrash(Exception exception)
    {
        try
        {
            CrashLog.Append(LocalDir, exception, DateTimeOffset.Now);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Günlük yazılamıyorsa yapacak bir şey yok; uygulamayı daha fazla bozmayalım.
        }
    }

    private void TryShowTaskbar()
    {
        try
        {
            _guard?.ShowAndMarkClean();
        }
        catch (Exception ex)
        {
            // En iyi çaba: bekçi süreç zaten yedek.
            Debug.WriteLine($"Görev çubuğu geri getirilemedi: {ex}");
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        TryShowTaskbar();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
