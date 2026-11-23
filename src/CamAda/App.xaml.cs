using System.Diagnostics;
using System.IO;
using System.Windows;
using CamAda.Core.Diagnostics;
using CamAda.Core.Safety;
using CamAda.Core.Settings;
using CamAda.Core.Startup;
using CamAda.Interop;
using CamAda.Island;
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
    private readonly List<DateTime> _watchdogStarts = new();
    private TaskbarSafetyStore _safety = null!;
    private SettingsStore _settingsStore = null!;
    private AppSettings _settings = null!;
    private bool _exiting;

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

            default:
                StartNormal();
                return;
        }
    }

    private void StartNormal()
    {
        _singleInstance = new Mutex(initiallyOwned: true, @"Local\CamAda.Main", out var createdNew);
        if (!createdNew)
        {
            Shutdown();
            return;
        }

        // Önceki oturum hem ana süreci hem bekçiyi kaybettiyse görev çubuğunu önce geri getir.
        if (WatchdogDecision.ShouldRestore(_safety.Load()))
        {
            RestoreRunner.RestoreTaskbar(_safety);
        }

        StartWatchdog();

        _settingsStore = new SettingsStore(RoamingDir);
        _settings = _settingsStore.Load();

        _shell = new ShellHost();
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

        _island = new IslandWindow(_strip, _settings, new IslandCommands(
            IsTaskbarHidden: () => _guard.IsHidden,
            SetTaskbarHidden: SetTaskbarHidden,
            Exit: ExitGracefully,
            GetGlassMode: () => _settings.GlassMode,
            SetGlassMode: mode =>
            {
                _settings = _settings with { GlassMode = mode };
                _settingsStore.Save(_settings);
            }));
        _island.Show();
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
