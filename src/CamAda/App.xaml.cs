using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using CamAda.Core.Apps;
using CamAda.Core.Diagnostics;
using CamAda.Core.Safety;
using CamAda.Core.Settings;
using CamAda.Core.Startup;
using CamAda.Interop;
using CamAda.Island;
using CamAda.Media;
using CamAda.Safety;
using CamAda.Shell;

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
    private DockViewModel? _dock;
    private NowPlayingService? _media;
    private readonly Panels.AppIndex _appIndex = new();
    private readonly List<DateTime> _watchdogStarts = new();
    private TaskbarSafetyStore _safety = null!;
    private SettingsStore _settingsStore = null!;
    private AppSettings _settings = null!;
    private bool _exiting;
    private bool _hostsTray;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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

        // Çalma durumu ada, panel ve plaktan önce hazır olsun (hepsi aynı servisi dinler).
        _media = await NowPlayingService.CreateAsync(Dispatcher);

        // Önceki oturum hem ana süreci hem bekçiyi kaybettiyse görev çubuğunu önce geri getir.
        if (WatchdogDecision.ShouldRestore(_safety.Load()))
        {
            RestoreRunner.RestoreTaskbar(_safety);
        }

        StartWatchdog();

        _settingsStore = new SettingsStore(RoamingDir);
        _settings = _settingsStore.Load();
        EnsurePinnedApps();
        ApplyAutostart(_settings.StartWithWindows);

        _hostsTray = _settings.HideWindowsTaskbar;
        _shell = new ShellHost(_hostsTray);
        _guard = new TaskbarGuard(_shell.Manager.ExplorerHelper, _safety);

        DispatcherUnhandledException += (_, e) => OnCrash(e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => OnCrash(e.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, e) => LogCrash(e.Exception);
        SessionEnding += (_, _) => ExitGracefully();

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
            IsStartWithWindows: () => _settings.StartWithWindows,
            SetStartWithWindows: SetStartWithWindows), _dock);
        _island.Bar.TrayClicked += (_, _) => OpenTrayPanel();
        _island.Bar.StartClicked += (_, _) => OpenStartPanel();
        _island.Bar.SearchClicked += (_, _) => OpenStartPanel();
        _island.LocationChanged += (_, _) => UpdateTrayHostSizeData(); // ekran ayarı değişince ada yeniden yerleşir
        _island.Show();
        _ = Dispatcher.BeginInvoke(UpdateTrayHostSizeData, DispatcherPriority.Loaded);

        _ = _appIndex.GetItemsAsync(); // ilk aramada beklememek için arka planda hazırla
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

    private void SetStartWithWindows(bool enabled)
    {
        _settings = _settings with { StartWithWindows = enabled };
        _settingsStore.Save(_settings);
        ApplyAutostart(enabled);
    }

    /// <summary>
    /// HKCU\...\Run altına çalışan exe'yi yazar ya da değeri siler. Her açılışta yeniden yazılır; uygulama taşınırsa
    /// (ör. kurulum betiğiyle) Run değeri yeni yeri gösterir.
    /// </summary>
    private static void ApplyAutostart(bool enabled)
    {
        try
        {
            using var run = Microsoft.Win32.Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run");
            if (enabled && Environment.ProcessPath is { } exe)
            {
                run.SetValue(AutostartCommand.RunValueName, AutostartCommand.Build(exe));
            }
            else
            {
                run.DeleteValue(AutostartCommand.RunValueName, throwOnMissingValue: false);
            }
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or IOException)
        {
            Debug.WriteLine($"Açılışta başlatma ayarlanamadı: {ex.Message}");
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
        _island?.Close();
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
