using System.Diagnostics;
using System.IO;
using System.Windows;
using CamAda.Core.Safety;
using CamAda.Core.Settings;
using CamAda.Core.Startup;
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

        DispatcherUnhandledException += (_, _) => TryShowTaskbar();
        AppDomain.CurrentDomain.UnhandledException += (_, _) => TryShowTaskbar();
        SessionEnding += (_, _) => ExitGracefully();

        if (_settings.HideWindowsTaskbar)
        {
            _guard.Hide();
        }
    }

    private static void StartWatchdog()
    {
        var exe = Environment.ProcessPath;
        if (exe is null)
        {
            return;
        }

        Process.Start(new ProcessStartInfo(exe, $"--watchdog {Environment.ProcessId}") { UseShellExecute = false })
            ?.Dispose();
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
        _shell?.Shutdown();
        Shutdown();
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
