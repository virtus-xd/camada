using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CamAda.Core.Glass;
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

    // Akıllı gizlenme (spec §7.6)
    private readonly DispatcherTimer _occlusionTimer = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly DispatcherTimer _cursorTimer = new(DispatcherPriority.Input) { Interval = TimeSpan.FromMilliseconds(50) };
    private readonly int _ownProcessId = Environment.ProcessId;
    private bool _fullScreen;
    private bool _occluded;
    private bool _cursorInside;
    private IslandPresence _presence = IslandPresence.Shown;

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

        _hover.ExpandedChanged += (_, expanded) => Dispatcher.BeginInvoke(() =>
        {
            AnimateTo(expanded);
            ApplyPresence();
        });

        // Örtülü değilken hapın fare olayları, örtülüyken imleç yoklaması HoverIntent'i besler.
        PillBorder.MouseEnter += (_, _) => { if (!_occluded) _hover.PointerEntered(); };
        PillBorder.MouseLeave += (_, _) => { if (!_occluded) _hover.PointerExited(); };

        _occlusionTimer.Tick += (_, _) => CheckOcclusion();
        _cursorTimer.Tick += (_, _) => PollCursor();

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
        _occlusionTimer.Start();
        CheckOcclusion();
    }

    /// <summary>Adanın yerini (üst orta) bir pencerenin kaplayıp kaplamadığını yoklar.</summary>
    private void CheckOcclusion()
    {
        if (_pinnedPosition is not { } pinned)
        {
            return;
        }

        // Adanın ekranda kapladığı alan: küçük hap + gölge payı (yanlarda 30, altta 38 DIP).
        const double shadowSide = 30, shadowBelow = 38;
        var dpi = VisualTreeHelper.GetDpi(this);
        var pillLeft = (IslandLayout.WindowWidth - IslandLayout.Compact.Width) / 2;
        var footprint = new RectD(
            pinned.X + (pillLeft - shadowSide) * dpi.DpiScaleX,
            pinned.Y,
            (IslandLayout.Compact.Width + 2 * shadowSide) * dpi.DpiScaleX,
            (IslandLayout.TopMargin + IslandLayout.Compact.Height + shadowBelow) * dpi.DpiScaleY);
        var occluded = OcclusionRule.IsOccluded(Native.SampleTopLevelWindows(_ownProcessId), footprint);

        if (occluded == _occluded)
        {
            return;
        }

        _occluded = occluded;
        if (_occluded)
        {
            _cursorInside = false;
            _cursorTimer.Start();
        }
        else
        {
            _cursorTimer.Stop();
            if (_cursorInside && !PillBorder.IsMouseOver)
            {
                _hover.PointerExited();
            }

            _cursorInside = false;
        }

        ApplyPresence();
    }

    /// <summary>Örtülüyken imleci yoklar: üst kenarda (ada genişliğinde) ya da hapın içindeyse "içeride".</summary>
    private void PollCursor()
    {
        if (_pinnedPosition is not { } pinned)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var screenTop = AppBarScreen.FromPrimaryScreen().Bounds.Top;
        var (x, y) = Native.GetCursorPosition();
        var windowRight = pinned.X + IslandLayout.WindowWidth * dpi.DpiScaleX;
        var atTopEdge = RevealZone.IsAtTopEdge(x, y, pinned.X, windowRight, screenTop);

        var overPill = false;
        if (_presence == IslandPresence.Shown && PillBorder.ActualWidth > 0)
        {
            var topLeft = PillBorder.PointToScreen(new Point(0, 0));
            const double margin = 4;
            overPill = x >= topLeft.X - margin && x <= topLeft.X + PillBorder.ActualWidth * dpi.DpiScaleX + margin
                    && y >= topLeft.Y - margin && y <= topLeft.Y + PillBorder.ActualHeight * dpi.DpiScaleY + margin;
        }

        var inside = atTopEdge || overPill;
        if (inside == _cursorInside)
        {
            return;
        }

        _cursorInside = inside;
        if (inside)
        {
            _hover.PointerEntered();
        }
        else
        {
            _hover.PointerExited();
        }
    }

    /// <summary>Tam ekran, örtülme ve genişleme durumuna göre adayı gizler, çizgiye indirir ya da gösterir.</summary>
    private void ApplyPresence()
    {
        var presence = IslandPresenceRule.Decide(_fullScreen, _occluded, _hover.IsExpanded);

        if (presence == IslandPresence.Hidden)
        {
            Hide();
            _presence = presence;
            return;
        }

        if (!IsVisible)
        {
            Show();
        }

        var animate = SystemParameters.ClientAreaAnimation;
        var duration = animate ? TimeSpan.FromSeconds(0.18) : TimeSpan.Zero;
        var tucked = presence == IslandPresence.Tucked;
        PillBorder.IsHitTestVisible = !tucked;

        // Katmanlı pencerede Windows tıklamayı piksel piksel yönlendirir; çizginin pikselleri tıklamayı yutmasın diye
        // çizgi hâlinde pencerenin tamamı tıklama geçirir. Açılma, imleç yoklamasıyla olur.
        Native.SetExtendedStyle(new WindowInteropHelper(this).Handle, Native.WS_EX_TRANSPARENT, on: tucked);
        PillBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(tucked ? 0 : 1, duration));
        TuckLine.BeginAnimation(OpacityProperty, new DoubleAnimation(tucked ? 1 : 0, duration));

        UpdateTint();
        _presence = presence;
    }

    /// <summary>Bir pencerenin üstünde açıldığında arkada duvar kağıdı yok: tonu koyulaştır (düz cam).</summary>
    private void UpdateTint() =>
        TintLayer.Background = _occluded
            ? (Brush)FindResource("SolidTintBrush")
            : _glassMode == GlassMode.Wallpaper ? (Brush)FindResource("IslandTintBrush") : Brushes.Transparent;

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
        UpdateTint();
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
        _fullScreen = isFullScreen;
        ApplyPresence();
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
        _occlusionTimer.Stop();
        _cursorTimer.Stop();
        _hover.Dispose();
        _backdrop?.Dispose();
        _wallpaper.Dispose();
        _strip.FullScreenChanged -= OnFullScreenChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
    }
}
