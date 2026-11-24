using System.Collections.Specialized;
using System.Windows.Threading;
using CamAda.Core.Glass;
using CamAda.Core.Island;
using CamAda.Interop;
using ManagedShell.AppBar;

namespace CamAda.Shell;

/// <summary>
/// Ana ekranın üst kenarında görünmez bir AppBar. Akıllı gizlenmeden beri alan ayırmaz (<see cref="AppBarMode.None"/>);
/// tam ekran bir uygulama açılınca <see cref="FullScreenChanged"/> olayını tetikler.
/// </summary>
public partial class StripAppBar : AppBarWindow
{
    private readonly DispatcherTimer _fullScreenRecheck = new(DispatcherPriority.Background)
    {
        Interval = TimeSpan.FromMilliseconds(250),
    };

    public StripAppBar(ShellHost host, double height)
        : base(
            host.Manager.AppBarManager,
            host.Manager.ExplorerHelper,
            host.Manager.FullScreenHelper,
            AppBarScreen.FromPrimaryScreen(),
            AppBarEdge.Top,
            AppBarMode.None, // Akıllı gizlenme: alan ayrılmaz (spec §7.6); yalnızca tam ekranı algılar.
            height)
    {
        InitializeComponent();

        _fullScreenHelper.FullScreenApps.CollectionChanged += OnFullScreenAppsChanged;
        _fullScreenRecheck.Tick += (_, _) => UpdateFullScreen();
        Closed += (_, _) =>
        {
            _fullScreenRecheck.Stop();
            _fullScreenHelper.FullScreenApps.CollectionChanged -= OnFullScreenAppsChanged;
        };
        UpdateFullScreen();
    }

    public bool IsFullScreenActive { get; private set; }

    public event EventHandler<bool>? FullScreenChanged;

    /// <summary>
    /// Explorer yeniden başlayınca ("TaskbarCreated") eski AppBar kaydı kaybolur. ManagedShell bunu Explorer'la birlikte
    /// çalışırken ele almadığı için kaydı burada yeniliyoruz. AppBarMode.None'da kayıt işlemleri etkisizdir; ileride
    /// yeniden alan ayırma kipine dönülürse diye korunur.
    /// </summary>
    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.TaskbarCreatedMessage && !AllowClose)
        {
            Dispatcher.BeginInvoke(() =>
            {
                UnregisterAppBar();
                RegisterAppBar();
                UpdatePosition();
            });
        }

        return base.WndProc(hwnd, msg, wParam, lParam, ref handled);
    }

    private void OnFullScreenAppsChanged(object? sender, NotifyCollectionChangedEventArgs e) => UpdateFullScreen();

    /// <summary>
    /// ManagedShell'in listesi yalnız adaydır; son kararı <see cref="FullScreenRule"/> verir. Görev çubuğu gizliyken
    /// büyütülmüş VS Code/Chrome listeye "tam ekran" diye girer ama adayı gizlememelidir. Liste yalnız pencere
    /// etkinleşince güncellendiğinden, listede pencere varken karar 250 ms'de bir yenilenir (ör. büyütülmüş Chrome'da
    /// video tam ekrana geçince ya da tam ekran pencere küçültülünce liste değişmez).
    /// </summary>
    private void UpdateFullScreen()
    {
        var apps = _fullScreenHelper.FullScreenApps;
        var active = apps.Any(app =>
            (app.screen.DeviceName == Screen.DeviceName || app.screen.IsVirtualScreen)
            && FullScreenRule.IsFullScreen(Native.SampleFullScreenCandidate(
                app.hWnd,
                new RectD(app.screen.Bounds.Left, app.screen.Bounds.Top, app.screen.Bounds.Width, app.screen.Bounds.Height),
                app.fromTasksService)));

        _fullScreenRecheck.IsEnabled = apps.Count > 0;

        if (active != IsFullScreenActive)
        {
            IsFullScreenActive = active;
            FullScreenChanged?.Invoke(this, active);
        }
    }
}
