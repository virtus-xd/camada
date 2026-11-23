using ManagedShell.AppBar;

namespace CamAda.Shell;

/// <summary>
/// Ana ekranın üst kenarında görünmez bir AppBar: ekranı kaplayan pencerelerin altından başlamasını sağlar
/// ve tam ekran bir uygulama açılınca <see cref="FullScreenChanged"/> olayını tetikler.
/// </summary>
public partial class StripAppBar : AppBarWindow
{
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
    }

    public bool IsFullScreenActive { get; private set; }

    /// <summary>
    /// Explorer yeniden başlayınca ("TaskbarCreated") eski AppBar kaydı kaybolur ve ayrılan alan sıfırlanır.
    /// ManagedShell bunu Explorer'la birlikte çalışırken ele almadığı için kaydı burada yeniliyoruz.
    /// </summary>
    protected override IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Interop.Native.TaskbarCreatedMessage && !AllowClose)
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

    public event EventHandler<bool>? FullScreenChanged;

    protected override void OnFullScreenEnter(FullScreenApp app)
    {
        base.OnFullScreenEnter(app);
        if (!Topmost && !IsFullScreenActive)
        {
            IsFullScreenActive = true;
            FullScreenChanged?.Invoke(this, true);
        }
    }

    protected override void OnFullScreenLeave()
    {
        base.OnFullScreenLeave();
        if (IsFullScreenActive)
        {
            IsFullScreenActive = false;
            FullScreenChanged?.Invoke(this, false);
        }
    }
}
