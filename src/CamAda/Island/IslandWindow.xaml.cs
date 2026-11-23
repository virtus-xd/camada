using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CamAda.Core.Island;
using CamAda.Core.Settings;
using CamAda.Core.Time;
using CamAda.Glass;
using CamAda.Interop;
using CamAda.Shell;
using ManagedShell.AppBar;
using Microsoft.Win32;

namespace CamAda.Island;

/// <summary>
/// Ana ekranın üst ortasında duran, saydam ve her zaman üstte cam ada penceresi.
/// Odağı başka pencerelerden çalmaz; saydam alanlara yapılan tıklamalar alttaki pencereye geçer.
/// </summary>
public partial class IslandWindow : Window
{
    private readonly StripAppBar _strip;
    private readonly AppSettings _settings;
    private readonly IslandCommands _commands;
    private readonly DispatcherTimer _clockTimer = new(DispatcherPriority.Normal);
    private readonly HoverIntent _hover;
    private readonly WallpaperSource _wallpaper;
    private IGlassBackdrop? _backdrop;
    private GlassMode _glassMode;
    private (int X, int Y)? _pinnedPosition;

    public IslandWindow(StripAppBar strip, AppSettings settings, IslandCommands commands)
    {
        InitializeComponent();
        _strip = strip;
        _settings = settings;
        _commands = commands;

        _strip.FullScreenChanged += OnFullScreenChanged;
        _clockTimer.Tick += (_, _) => RefreshClock();
        SourceInitialized += OnSourceInitialized;
        DpiChanged += (_, _) => PlaceOnPrimaryScreen();
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
        _wallpaper = new WallpaperSource(Dispatcher);
        _hover = new HoverIntent(
            TimeProvider.System,
            TimeSpan.FromMilliseconds(_settings.HoverEnterDelayMs),
            TimeSpan.FromMilliseconds(_settings.HoverLeaveDelayMs));

        PillBorder.ContextMenuOpening += (_, _) =>
        {
            HideTaskbarItem.IsChecked = _commands.IsTaskbarHidden();
            GlassWallpaperItem.IsChecked = _glassMode == GlassMode.Wallpaper;
            GlassAcrylicItem.IsChecked = _glassMode == GlassMode.Acrylic;
            GlassSolidItem.IsChecked = _glassMode == GlassMode.Solid;
            _hover.SetPanelOpen(true); // menü açıkken ada küçülmesin
        };
        PillBorder.ContextMenuClosing += (_, _) => _hover.SetPanelOpen(false);

        _hover.ExpandedChanged += (_, expanded) => Dispatcher.BeginInvoke(() => AnimateTo(expanded));
        PillBorder.MouseEnter += (_, _) => _hover.PointerEntered();
        PillBorder.MouseLeave += (_, _) => _hover.PointerExited();

        Closed += OnClosed;

        RefreshClock();
    }

    /// <summary>Adanın hap şeklindeki gövdesi (genişleme ve cam görünümü bunu kullanır).</summary>
    public Border Pill => PillBorder;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        Native.AddExtendedStyle(handle, Native.WS_EX_NOACTIVATE);
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        PlaceOnPrimaryScreen();
        ApplyGlass(_commands.GetGlassMode());
    }

    /// <summary>Cam görünümünü değiştirir: önceki yöntemi geri alır, yenisini uygular.</summary>
    public void ApplyGlass(GlassMode mode)
    {
        _backdrop?.Dispose();
        _backdrop = null;
        PillBorder.Background = (Brush)FindResource("IslandTintBrush");
        TintLayer.Background = Brushes.Transparent;

        switch (mode)
        {
            case GlassMode.Wallpaper:
                _backdrop = new WallpaperGlass(_wallpaper, TintLayer);
                break;
            case GlassMode.Acrylic:
                _backdrop = new AcrylicGlass();
                break;
            default:
                PillBorder.Background = (Brush)FindResource("SolidTintBrush");
                break;
        }

        _backdrop?.Attach(this, PillBorder);
        _glassMode = mode;
    }

    /// <summary>Pencereyi ana ekranın üst kenarında yatayda ortalar ve o konuma sabitler.</summary>
    private void PlaceOnPrimaryScreen()
    {
        var bounds = AppBarScreen.FromPrimaryScreen().Bounds; // fiziksel piksel
        var dpi = VisualTreeHelper.GetDpi(this);
        var widthPx = IslandLayout.WindowWidth * dpi.DpiScaleX;
        _pinnedPosition = (
            (int)Math.Round(bounds.Left + (bounds.Width - widthPx) / 2),
            bounds.Top);

        Left = _pinnedPosition.Value.X / dpi.DpiScaleX;
        Top = _pinnedPosition.Value.Y / dpi.DpiScaleY;
    }

    /// <summary>
    /// Görev çubuğu gizlenince ya da çalışma alanı değişince Windows, çalışma alanı dışında kalan pencereleri
    /// içeri iter. Ada bilerek çalışma alanının dışında (ayrılmış üst şeritte) durduğu için her taşıma isteğini
    /// sabit konuma çeviririz.
    /// </summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_WINDOWPOSCHANGING && _pinnedPosition is { } pinned)
        {
            var pos = Marshal.PtrToStructure<Native.WINDOWPOS>(lParam);
            if ((pos.flags & Native.SWP_NOMOVE) == 0 && (pos.x != pinned.X || pos.y != pinned.Y))
            {
                pos.x = pinned.X;
                pos.y = pinned.Y;
                Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
            }
        }

        return IntPtr.Zero;
    }

    /// <summary>Hapı küçük ya da geniş ölçüye esneyerek götürür ve iki içerik katmanı arasında geçiş yapar.</summary>
    private void AnimateTo(bool expanded)
    {
        var target = expanded ? IslandLayout.Expanded : IslandLayout.Compact;
        var animate = SystemParameters.ClientAreaAnimation; // "Animasyon efektleri" kapalıysa anında
        var sizeDuration = animate ? TimeSpan.FromSeconds(expanded ? 0.4 : 0.3) : TimeSpan.Zero;
        IEasingFunction easing = expanded
            ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.15 }
            : new CubicEase { EasingMode = EasingMode.EaseOut };

        PillBorder.BeginAnimation(WidthProperty, new DoubleAnimation(target.Width, sizeDuration) { EasingFunction = easing });
        PillBorder.BeginAnimation(HeightProperty, new DoubleAnimation(target.Height, sizeDuration) { EasingFunction = easing });

        var outgoing = expanded ? CompactContent : ExpandedContent;
        var incoming = expanded ? ExpandedContent : CompactContent;
        outgoing.IsHitTestVisible = false;
        incoming.IsHitTestVisible = true;
        outgoing.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0, animate ? TimeSpan.FromSeconds(0.16) : TimeSpan.Zero));
        incoming.BeginAnimation(OpacityProperty,
            new DoubleAnimation(1, animate ? TimeSpan.FromSeconds(0.22) : TimeSpan.Zero)
            {
                BeginTime = animate ? TimeSpan.FromSeconds(0.06) : TimeSpan.Zero,
            });
    }

    private void RefreshClock()
    {
        var now = DateTime.Now;
        CompactDate.Text = ExpandedDate.Text = ClockFormatter.Date(now);
        CompactTime.Text = ExpandedTime.Text = ClockFormatter.Time(now);

        _clockTimer.Stop();
        _clockTimer.Interval = MinuteTicker.DelayToNextMinute(now) + TimeSpan.FromMilliseconds(30);
        _clockTimer.Start();
    }

    private void OnFullScreenChanged(object? sender, bool isFullScreen)
    {
        if (isFullScreen)
        {
            Hide();
        }
        else
        {
            Show();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) =>
        Dispatcher.BeginInvoke(PlaceOnPrimaryScreen);

    private void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            Dispatcher.BeginInvoke(RefreshClock);
        }
    }

    private void OnTimeChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(RefreshClock);

    private void HideTaskbarItem_Click(object sender, RoutedEventArgs e) =>
        _commands.SetTaskbarHidden(HideTaskbarItem.IsChecked);

    private void ExitItem_Click(object sender, RoutedEventArgs e) => _commands.Exit();

    private void GlassItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem { Tag: string tag } && Enum.TryParse<GlassMode>(tag, out var mode))
        {
            ApplyGlass(mode);
            _commands.SetGlassMode(mode);
        }
    }

    private void OnClosed(object? sender, EventArgs e)
    {
        _clockTimer.Stop();
        _hover.Dispose();
        _backdrop?.Dispose();
        _wallpaper.Dispose();
        _strip.FullScreenChanged -= OnFullScreenChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
    }
}
