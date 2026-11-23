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
using CamAda.Panels;
using CamAda.Shell;
using ManagedShell.AppBar;
using Microsoft.Win32;

namespace CamAda.Island;

/// <summary>
/// Ana ekranın üst ortasında duran, saydam ve her zaman üstte cam ada penceresi.
/// Odağı başka pencerelerden çalmaz; saydam alanlara yapılan tıklamalar alttaki pencereye geçer.
/// Yerleşim, saat, genişleme animasyonu, cam ve menü burada; akıllı gizlenme <see cref="SmartHide"/>'da.
/// </summary>
public partial class IslandWindow : Window
{
    private readonly StripAppBar _strip;
    private readonly AppSettings _settings;
    private readonly IslandCommands _commands;
    private readonly DockViewModel _dock;
    private readonly DispatcherTimer _clockTimer = new(DispatcherPriority.Normal);
    private readonly HoverIntent _hover;
    private readonly SmartHide _smartHide;
    private readonly WallpaperSource _wallpaper;
    private readonly PanelHost _panels;
    private IGlassBackdrop? _backdrop;
    private GlassMode _glassMode;
    private (int X, int Y)? _pinnedPosition;
    private bool _fullScreen;
    private bool _closed;

    public IslandWindow(StripAppBar strip, AppSettings settings, IslandCommands commands, DockViewModel dock)
    {
        InitializeComponent();
        _strip = strip;
        _settings = settings;
        _commands = commands;
        _dock = dock;
        ExpandedContent.AppsSource = _dock.Items;
        ExpandedContent.Width = ExpandedTargetWidth - 2;

        _strip.FullScreenChanged += OnFullScreenChanged;
        _clockTimer.Tick += (_, _) => RefreshClock();
        SourceInitialized += OnSourceInitialized;
        DpiChanged += (_, e) =>
        {
            if (WindowDpi.IsRealChange(this, e))
            {
                PlaceOnPrimaryScreen();
            }
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        SystemEvents.TimeChanged += OnTimeChanged;
        _wallpaper = new WallpaperSource(Dispatcher);
        _hover = new HoverIntent(
            TimeProvider.System,
            TimeSpan.FromMilliseconds(_settings.HoverEnterDelayMs),
            TimeSpan.FromMilliseconds(_settings.HoverLeaveDelayMs));
        _smartHide = new SmartHide(this, _hover, () => _pinnedPosition, () => PillBorder);
        _smartHide.StateChanged += (_, _) => ApplyPresence();

        _panels = new PanelHost(this, _hover);
        var actions = new DockActions(_dock, _panels);
        ExpandedContent.AppClicked += (_, e) => actions.Click(e.Item, e.Button);
        ExpandedContent.AppMiddleClicked += (_, e) => actions.MiddleClick(e.Item, e.Button);

        _dock.Changed += (_, _) =>
        {
            ExpandedContent.Width = ExpandedTargetWidth - 2;

            // Satır değişince açık ada yeni genişliğe kısa bir geçişle uyar.
            if (_hover.IsExpanded && !_closed)
            {
                AnimateWidth(ExpandedTargetWidth, TimeSpan.FromSeconds(0.2), new CubicEase { EasingMode = EasingMode.EaseOut });
            }
        };

        PillBorder.ContextMenuOpening += (_, _) =>
        {
            HideTaskbarItem.IsChecked = _commands.IsTaskbarHidden();
            GlassWallpaperItem.IsChecked = _glassMode == GlassMode.Wallpaper;
            GlassAcrylicItem.IsChecked = _glassMode == GlassMode.Acrylic;
            GlassSolidItem.IsChecked = _glassMode == GlassMode.Solid;
            _hover.SetPanelOpen(true); // menü açıkken ada küçülmesin
        };
        PillBorder.ContextMenuClosing += (_, _) => _hover.SetPanelOpen(false);

        // Olay zamanlayıcı iş parçacığından gelir; sıra karışabileceği için gönderim anındaki gerçek durum kullanılır.
        _hover.ExpandedChanged += (_, _) => Dispatcher.BeginInvoke(() =>
        {
            if (_closed)
            {
                return;
            }

            AnimateTo(_hover.IsExpanded);
            ApplyPresence();
        });

        // Örtülü değilken hapın fare olayları, örtülüyken SmartHide'ın imleç yoklaması HoverIntent'i besler.
        PillBorder.MouseEnter += (_, _) => { if (!_smartHide.IsOccluded) _hover.PointerEntered(); };
        PillBorder.MouseLeave += (_, _) => { if (!_smartHide.IsOccluded) _hover.PointerExited(); };

        Closed += OnClosed;

        RefreshClock();
    }

    /// <summary>Adanın hap şeklindeki gövdesi (genişleme ve cam görünümü bunu kullanır).</summary>
    public Border Pill => PillBorder;

    /// <summary>Genişleme niyeti; paneller açıkken <see cref="HoverIntent.SetPanelOpen"/> ile adayı büyük tutar.</summary>
    public HoverIntent Hover => _hover;

    /// <summary>Genişlemiş satır (Başlat, Ara, uygulamalar, ^ olayları için).</summary>
    public IslandBar Bar => ExpandedContent;

    /// <summary>Adanın altındaki paneller (tek panel kuralı; açıkken ada büyük kalır).</summary>
    public PanelHost Panels => _panels;

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        Native.AddExtendedStyle(handle, Native.WS_EX_NOACTIVATE);
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        PlaceOnPrimaryScreen();
        ApplyGlass(_commands.GetGlassMode());
        _smartHide.Start();
    }

    /// <summary>Tam ekran, örtülme ve genişleme durumuna göre adayı gizler, çizgiye indirir ya da gösterir.</summary>
    private void ApplyPresence()
    {
        if (_closed)
        {
            return;
        }

        var presence = IslandPresenceRule.Decide(_fullScreen, _smartHide.IsOccluded, _hover.IsExpanded);

        if (presence == IslandPresence.Hidden)
        {
            Hide();
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
    }

    /// <summary>Bir pencerenin üstünde açıldığında arkada duvar kağıdı yok: tonu koyulaştır (düz cam).</summary>
    private void UpdateTint() =>
        TintLayer.Background = _smartHide.IsOccluded
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
    /// Görev çubuğu gizlenince ya da çalışma alanı değişince Windows, ekranın en üst kenarındaki pencereleri
    /// aşağı itebilir. Ada bilerek ekranın en üstünde durduğu için her taşıma isteğini sabit konuma çeviririz.
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

    /// <summary>Genişlemiş hapın, satırdaki uygulama düğmesi sayısına göre genişliği.</summary>
    private double ExpandedTargetWidth => IslandLayout.ExpandedWidthFor(_dock.Items.Count);

    private void AnimateWidth(double width, TimeSpan duration, IEasingFunction easing)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            duration = TimeSpan.Zero;
        }

        PillBorder.BeginAnimation(WidthProperty, new DoubleAnimation(width, duration) { EasingFunction = easing });
    }

    /// <summary>Hapı küçük ya da geniş ölçüye esneyerek götürür ve iki içerik katmanı arasında geçiş yapar.</summary>
    private void AnimateTo(bool expanded)
    {
        var height = expanded ? IslandLayout.Expanded.Height : IslandLayout.Compact.Height;
        var width = expanded ? ExpandedTargetWidth : IslandLayout.Compact.Width;
        var animate = SystemParameters.ClientAreaAnimation; // "Animasyon efektleri" kapalıysa anında
        var sizeDuration = animate ? TimeSpan.FromSeconds(expanded ? 0.4 : 0.3) : TimeSpan.Zero;
        IEasingFunction easing = expanded
            ? new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.15 }
            : new CubicEase { EasingMode = EasingMode.EaseOut };

        AnimateWidth(width, sizeDuration, easing);
        PillBorder.BeginAnimation(HeightProperty, new DoubleAnimation(height, sizeDuration) { EasingFunction = easing });

        UIElement outgoing = expanded ? CompactContent : ExpandedContent;
        UIElement incoming = expanded ? ExpandedContent : CompactContent;
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
        CompactDate.Text = ClockFormatter.Date(now);
        CompactTime.Text = ClockFormatter.Time(now);
        ExpandedContent.SetTime(CompactTime.Text);

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
        _closed = true;
        _panels.Close();
        _clockTimer.Stop();
        _smartHide.Dispose();
        _hover.Dispose();
        _backdrop?.Dispose();
        _wallpaper.Dispose();
        _strip.FullScreenChanged -= OnFullScreenChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
        SystemEvents.TimeChanged -= OnTimeChanged;
    }
}
