using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CamAda.Core.Glass;
using CamAda.Core.Island;
using CamAda.Core.Media;
using CamAda.Core.Record;
using CamAda.Core.Settings;
using CamAda.Glass;
using CamAda.Interop;
using CamAda.Media;
using ManagedShell.AppBar;
using Microsoft.Win32;

namespace CamAda.Record;

/// <summary>
/// Masaüstündeki plak çalar (spec §7.8): ana ekranın sol kenarında, pencerelerin arkasında (z-sırasında en altta), görev
/// çubuğunda ve Alt+Tab'da olmayan saydam pencere. Tıklamak ya da odaktayken Enter/Boşluk çal/duraklat yapar ama plağı öne
/// getirmez. Dönüş yalnız müzik çalarken (ya da yavaşlarken) ve plak tamamen örtülü değilken çizilir.
/// </summary>
public sealed partial class RecordWindow : Window
{
    private static readonly TimeSpan VisibilityPoll = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan CardFade = TimeSpan.FromSeconds(0.15);

    /// <summary>Kartın yuvası, kartın uzayabilmesi için RecordLayout'taki yerinin üstüne ve altına bu kadar taşar.</summary>
    private const double CardGrowth = 40;

    private readonly NowPlayingService _media;
    private readonly Func<RecordSettings> _settings;
    private readonly RecordSpin _spin = new();
    private readonly Action<TimeSpan> _onFrame;
    private readonly DispatcherTimer _visibilityTimer;
    private readonly int _ownProcessId = Environment.ProcessId;
    private readonly RecordCard _card;
    private readonly TranslateTransform _cardSlide = new();
    private readonly HoverIntent _hover = new(TimeProvider.System, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(300));
    private readonly WallpaperSource _wallpaper;
    private WallpaperGlass? _glass;
    private RecordGeometry _geometry;
    private bool _cardOpen;
    private bool _keyboardFocus;
    private bool _covered;
    private bool _rendering;
    private bool _closed;

    public RecordWindow(NowPlayingService media, Func<RecordSettings> settings)
    {
        InitializeComponent();
        _media = media;
        _settings = settings;
        _onFrame = OnFrame;
        _visibilityTimer = new DispatcherTimer(VisibilityPoll, DispatcherPriority.Background, (_, _) => CheckCovered(), Dispatcher);
        _visibilityTimer.Stop();

        SourceInitialized += OnSourceInitialized;
        DpiChanged += (_, e) =>
        {
            if (WindowDpi.IsRealChange(this, e))
            {
                Place();
            }
        };
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
        _media.Changed += OnMediaChanged;
        IsVisibleChanged += (_, _) => UpdateAnimation(); // gösterilince dönüş bir sonraki medya olayını beklemesin
        Closed += OnClosed;

        Disc.Invoked += async (_, _) => await _media.TogglePlayPauseAsync();
        // Tıklama: diskin üstünde basılıp yine diskin dairesinde bırakılınca (başka yerde basılıp sürüklenince değil).
        Disc.MouseLeftButtonDown += (_, e) =>
        {
            e.Handled = true;
            Disc.CaptureMouse();
        };
        Disc.MouseLeftButtonUp += (_, e) =>
        {
            if (!Disc.IsMouseCaptured)
            {
                return;
            }

            e.Handled = true;
            Disc.ReleaseMouseCapture();
            var point = e.GetPosition(Disc);
            if (DiscHit.Contains(point.X, point.Y, Disc.ActualWidth, Disc.ActualHeight))
            {
                Disc.Invoke();
            }
        };
        Disc.KeyDown += (_, e) =>
        {
            if (e.Key is Key.Enter or Key.Space)
            {
                e.Handled = true;
                Disc.Invoke();
            }
        };
        Disc.GotKeyboardFocus += (_, _) => FocusRing.Visibility = Visibility.Visible;
        Disc.LostKeyboardFocus += (_, _) => FocusRing.Visibility = Visibility.Collapsed;

        _card = new RecordCard(media) { Visibility = Visibility.Hidden, Opacity = 0, RenderTransform = _cardSlide };
        CardSlot.Children.Add(_card);
        CardShadow.SetBinding(HeightProperty, new Binding(nameof(ActualHeight)) { Source = _card });
        _wallpaper = new WallpaperSource(Dispatcher);

        // Plak, köprü ve kart tek bir "üstünde" niyetini besler: plaktan karta geçerken kart kapanmaz (spec §7.8).
        foreach (var element in new UIElement[] { Disc, CardBridge, _card })
        {
            element.MouseEnter += (_, _) => _hover.PointerEntered();
            element.MouseLeave += (_, _) => _hover.PointerExited();
        }

        _hover.ExpandedChanged += (_, _) => Dispatcher.BeginInvoke(UpdateCard);

        // Plak ya da kart klavyeyle odaklanınca kart açılır. Fareyle tıklanan düğme de odağı alır; o odak kartı açık
        // tutmaz (yoksa karttaki bir düğmeye tıkladıktan sonra fare çekilince kart kapanmazdı).
        Root.GotKeyboardFocus += (_, _) =>
        {
            _keyboardFocus = InputManager.Current.MostRecentInputDevice is KeyboardDevice;
            UpdateCard();
        };
        Root.IsKeyboardFocusWithinChanged += (_, _) =>
        {
            if (!Root.IsKeyboardFocusWithin)
            {
                _keyboardFocus = false;
            }

            UpdateCard();
        };

        UpdateMedia();
    }

    /// <summary>Plak gösteriliyor ve bir pencere tarafından tamamen örtülmüyor.</summary>
    public bool IsRecordVisible => IsVisible && !_covered;

    public event EventHandler? VisibilityChanged;

    /// <summary>Plağın müzik kartına iletilecek Spotify ekleri (beğen, sıradaki).</summary>
    public ISpotifyExtras? Extras
    {
        get => _card.Extras;
        set => _card.Extras = value;
    }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        Native.AddExtendedStyle(handle, Native.WS_EX_TOOLWINDOW); // Alt+Tab ve Görev Görünümü'nde yok
        Native.OwnByDesktop(handle);                               // Win+D'de masaüstüyle birlikte görünür kalır
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        Place();
        _glass = new WallpaperGlass(_wallpaper, _card.TintLayer); // kart masaüstünün üstünde: duvar kağıdı camı
        _glass.Attach(this, _card);
    }

    /// <summary>Ana ekranın fiziksel sınırlarını DIP'e çevirip <see cref="RecordLayout"/> ile yerleşir.</summary>
    private void Place()
    {
        var bounds = AppBarScreen.FromPrimaryScreen().Bounds; // fiziksel piksel
        var dpi = VisualTreeHelper.GetDpi(this);
        var screen = new RectD(bounds.Left / dpi.DpiScaleX, bounds.Top / dpi.DpiScaleY,
            bounds.Width / dpi.DpiScaleX, bounds.Height / dpi.DpiScaleY);
        var settings = _settings();
        _geometry = RecordLayout.Compute(screen, settings);

        Left = _geometry.Window.X;
        Top = _geometry.Window.Y;
        Width = _geometry.Window.Width;
        Height = _geometry.Window.Height;

        Canvas.SetLeft(Disc, _geometry.Disc.X);
        Canvas.SetTop(Disc, _geometry.Disc.Y);
        Disc.Width = Disc.Height = _geometry.Disc.Width;
        Visual.Configure(_geometry.Disc.Width, settings.EffectiveLabelDiameter, dpi.DpiScaleX);

        var card = _geometry.Card;
        var slotTop = Math.Max(0, card.Y - CardGrowth);
        var slotBottom = Math.Min(_geometry.Window.Height, card.Y + card.Height + CardGrowth);
        Canvas.SetLeft(CardSlot, card.X);
        Canvas.SetTop(CardSlot, slotTop);
        CardSlot.Width = card.Width;
        CardSlot.Height = slotBottom - slotTop;
        CardShadow.Width = card.Width;

        var bridgeLeft = _geometry.VisibleDisc.X + _geometry.VisibleDisc.Width - 1;
        Canvas.SetLeft(CardBridge, bridgeLeft);
        Canvas.SetTop(CardBridge, card.Y);
        CardBridge.Width = card.X - bridgeLeft + 1;
        CardBridge.Height = card.Height;
    }

    /// <summary>
    /// Fare plağın, köprünün ya da kartın üstündeyken (0,1 sn gecikmeyle) ya da odak plak veya karttayken kart açıktır;
    /// ikisinden de çıkınca 0,3 sn sonra kapanır. Açılırken 0,15 sn'de belirip 8 DIP sağa kayar; "animasyonları azalt"ta anında.
    /// </summary>
    private void UpdateCard()
    {
        var open = !_closed && (_hover.IsExpanded || (Root.IsKeyboardFocusWithin && _keyboardFocus));
        if (open == _cardOpen)
        {
            return;
        }

        _cardOpen = open;
        var animate = SystemParameters.ClientAreaAnimation;
        var duration = animate ? CardFade : TimeSpan.Zero;
        if (open)
        {
            foreach (var element in new UIElement[] { _card, CardShadow, CardBridge })
            {
                element.Visibility = Visibility.Visible;
            }

            _card.BeginAnimation(OpacityProperty, new DoubleAnimation(1, duration));
            CardShadow.BeginAnimation(OpacityProperty, new DoubleAnimation(1, duration));
            _cardSlide.BeginAnimation(TranslateTransform.XProperty, animate ? new DoubleAnimation(-8, 0, duration) : null);
            _ = Extras?.RefreshAsync();
            return;
        }

        var fade = new DoubleAnimation(0, duration);
        fade.Completed += (_, _) =>
        {
            if (!_cardOpen)
            {
                foreach (var element in new UIElement[] { _card, CardShadow, CardBridge })
                {
                    element.Visibility = Visibility.Hidden;
                }
            }
        };
        _card.BeginAnimation(OpacityProperty, fade);
        CardShadow.BeginAnimation(OpacityProperty, new DoubleAnimation(0, duration));
    }

    /// <summary>Pencere her zaman z-sırasının en altında kalır: tıklanınca ya da etkinleşince bile pencerelerin önüne geçmez.</summary>
    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_WINDOWPOSCHANGING)
        {
            var pos = Marshal.PtrToStructure<Native.WINDOWPOS>(lParam);
            if (pos.hwndInsertAfter != Native.HWND_BOTTOM || (pos.flags & Native.SWP_NOZORDER) != 0)
            {
                pos.hwndInsertAfter = Native.HWND_BOTTOM;
                pos.flags &= ~Native.SWP_NOZORDER;
                Marshal.StructureToPtr(pos, lParam, fDeleteOld: false);
            }
        }

        return IntPtr.Zero;
    }

    private void OnMediaChanged(object? sender, EventArgs e) => UpdateMedia();

    private bool Playing => _media.Current?.State == PlaybackState.Playing;

    private void UpdateMedia()
    {
        Visual.ShowCover(_media.Cover, _media.CoverColor);

        UpdateAnimation();
    }

    /// <summary>
    /// Dönüş yalnız çalarken ya da yavaşlarken ve plak görünürken çizilir; örtülme yoklaması yalnız o sırada sürer.
    /// Çizilmiyorken hız hemen hedefe oturur: örtülüyken duraklatılan plak tam hızda kalıp yoklamayı açık tutmaz.
    /// </summary>
    private void UpdateAnimation()
    {
        if ((Playing || _spin.IsMoving) && !_closed && !_visibilityTimer.IsEnabled)
        {
            _visibilityTimer.Start();
            RefreshCovered(); // yoklama dururken örtülme bilgisi eskidi
        }

        var drawable = IsRecordVisible && SystemParameters.ClientAreaAnimation && !_closed;
        if (!drawable)
        {
            _spin.Settle(Playing);
        }

        var active = Playing || _spin.IsMoving;
        if (!active || _closed)
        {
            _visibilityTimer.Stop();
        }

        var wanted = active && drawable;
        if (wanted && !_rendering)
        {
            _rendering = true;
            AnimationTicker.Add(_onFrame);
        }
        else if (!wanted && _rendering)
        {
            _rendering = false;
            AnimationTicker.Remove(_onFrame);
        }
    }

    private void OnFrame(TimeSpan elapsed)
    {
        _spin.Advance(elapsed, Playing, reduceMotion: !SystemParameters.ClientAreaAnimation);
        Visual.Angle = _spin.Angle;
        if (!Playing && !_spin.IsMoving)
        {
            UpdateAnimation();
        }
    }

    /// <summary>Plağın görünen kısmı bir pencereyle tamamen örtülüyse dönüş çizimini durdurur.</summary>
    private void CheckCovered()
    {
        if (RefreshCovered())
        {
            UpdateAnimation();
        }
    }

    /// <summary>Örtülme bilgisini tazeler; değiştiyse true.</summary>
    private bool RefreshCovered()
    {
        if (PresentationSource.FromVisual(this) is null)
        {
            return false;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var visible = new RectD(
            (_geometry.Window.X + _geometry.VisibleDisc.X) * dpi.DpiScaleX,
            (_geometry.Window.Y + _geometry.VisibleDisc.Y) * dpi.DpiScaleY,
            _geometry.VisibleDisc.Width * dpi.DpiScaleX,
            _geometry.VisibleDisc.Height * dpi.DpiScaleY);
        var self = new WindowInteropHelper(this).Handle;
        var covered = RecordVisibility.IsCovered(Native.SampleTopLevelWindows(_ownProcessId, self), visible);
        if (covered == _covered)
        {
            return false;
        }

        _covered = covered;
        VisibilityChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Place);

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _visibilityTimer.Stop();
        UpdateAnimation();
        _media.Changed -= OnMediaChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
        _hover.Dispose();
        _glass?.Dispose();
        _wallpaper.Dispose();
    }
}
