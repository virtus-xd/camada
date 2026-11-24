using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using CamAda.Core.Glass;
using CamAda.Core.Media;
using CamAda.Core.Record;
using CamAda.Core.Settings;
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

    private readonly NowPlayingService _media;
    private readonly Func<RecordSettings> _settings;
    private readonly RecordSpin _spin = new();
    private readonly Action<TimeSpan> _onFrame;
    private readonly DispatcherTimer _visibilityTimer;
    private readonly int _ownProcessId = Environment.ProcessId;
    private RecordGeometry _geometry;
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
        Closed += OnClosed;

        Disc.Invoked += async (_, _) => await _media.TogglePlayPauseAsync();
        Disc.MouseLeftButtonUp += (_, e) =>
        {
            e.Handled = true;
            Disc.Invoke();
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

        UpdateMedia();
    }

    /// <summary>Plak gösteriliyor ve bir pencere tarafından tamamen örtülmüyor.</summary>
    public bool IsRecordVisible => IsVisible && !_covered;

    public event EventHandler? VisibilityChanged;

    /// <summary>Plağın müzik kartına iletilecek Spotify ekleri (Görev 7–8).</summary>
    public ISpotifyExtras? Extras { get; set; }

    private void OnSourceInitialized(object? sender, EventArgs e)
    {
        var handle = new WindowInteropHelper(this).Handle;
        Native.AddExtendedStyle(handle, Native.WS_EX_TOOLWINDOW); // Alt+Tab ve Görev Görünümü'nde yok
        Native.OwnByDesktop(handle);                               // Win+D'de masaüstüyle birlikte görünür kalır
        HwndSource.FromHwnd(handle)?.AddHook(WndProc);
        Place();
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

    /// <summary>Dönüş yalnız çalarken ya da yavaşlarken ve plak görünürken; örtülme yoklaması yalnız çalarken.</summary>
    private void UpdateAnimation()
    {
        var active = Playing || _spin.IsMoving;
        if (active && !_closed)
        {
            _visibilityTimer.Start();
        }
        else
        {
            _visibilityTimer.Stop();
        }

        var wanted = active && IsRecordVisible && SystemParameters.ClientAreaAnimation && !_closed;
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
        if (PresentationSource.FromVisual(this) is null)
        {
            return;
        }

        var dpi = VisualTreeHelper.GetDpi(this);
        var visible = new RectD(
            (_geometry.Window.X + _geometry.VisibleDisc.X) * dpi.DpiScaleX,
            (_geometry.Window.Y + _geometry.VisibleDisc.Y) * dpi.DpiScaleY,
            _geometry.VisibleDisc.Width * dpi.DpiScaleX,
            _geometry.VisibleDisc.Height * dpi.DpiScaleY);
        var covered = RecordVisibility.IsCovered(Native.SampleTopLevelWindows(_ownProcessId), visible);
        if (covered != _covered)
        {
            _covered = covered;
            UpdateAnimation();
            VisibilityChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Dispatcher.BeginInvoke(Place);

    private void OnClosed(object? sender, EventArgs e)
    {
        _closed = true;
        _visibilityTimer.Stop();
        UpdateAnimation();
        _media.Changed -= OnMediaChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }
}
