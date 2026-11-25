using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CamAda.Assistant;
using CamAda.Core.Assistant;
using CamAda.Core.Island;
using CamAda.Core.Settings;
using CamAda.Core.Time;
using CamAda.Glass;
using System.Globalization;
using CamAda.Core.Media;
using CamAda.Core.Weather;
using CamAda.Interop;
using CamAda.Media;
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
    private readonly NowPlayingService _media;
    private readonly DispatcherTimer _graceTimer = new(DispatcherPriority.Normal);
    private CompactView _compactView = CompactView.Idle;
    private WeatherNow? _weather;
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

    /// <summary>Kısa gösterge: son <see cref="Flash"/> çağrısından bu kadar sonra kaybolur.</summary>
    private static readonly TimeSpan FlashHold = TimeSpan.FromSeconds(1.5);

    private readonly DispatcherTimer _flashTimer = new(DispatcherPriority.Normal);
    private FlashKind _flashKind;
    private AdaState _ada = new AdaState.Idle();
    private bool _adaShown;
    private bool _adaSuppressing;
    private bool _adaPulsing;
    private TimeSpan _adaPulseTime;
    private Action<TimeSpan>? _adaFrame;

    /// <summary>Ekran görüntüsü önizlemesi bu kadar sonra kaybolur; imleç üzerindeyken durur, çekilince 1 sn sonra.</summary>
    private static readonly TimeSpan ShotHold = TimeSpan.FromSeconds(5);

    private readonly DispatcherTimer _shotTimer = new(DispatcherPriority.Normal);
    private Func<Task<bool>>? _shotCopy;
    private Action? _shotEdit;

    /// <summary>Hapın genişlik animasyonunun en son hedefi (DIP).</summary>
    private double _widthTarget;

    public IslandWindow(StripAppBar strip, AppSettings settings, IslandCommands commands, DockViewModel dock,
        NowPlayingService media)
    {
        InitializeComponent();
        _strip = strip;
        _settings = settings;
        _commands = commands;
        _dock = dock;
        _media = media;
        _media.Changed += OnMediaChanged;
        _graceTimer.Tick += (_, _) => UpdateCompactView();
        ExpandedContent.AppsSource = _dock.Items;
        ExpandedContent.Width = ExpandedTargetWidth - 2;

        _fullScreen = _strip.IsFullScreenActive;
        _strip.FullScreenChanged += OnFullScreenChanged;
        _clockTimer.Tick += (_, _) => RefreshClock();
        _flashTimer.Interval = FlashHold;
        _flashTimer.Tick += (_, _) => EndFlash(immediate: false);
        _shotTimer.Tick += (_, _) => EndScreenshot();
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
        ExpandedContent.MusicClicked += (_, _) =>
            _panels.Toggle("music", () => MusicPanel.Create(_media, MusicExtras, MusicFooter?.Invoke()), MusicPanel.Width);
        ExpandedContent.ClockClicked += (_, _) =>
            _panels.Toggle("calendar", () => new CalendarPanel(DateOnly.FromDateTime(DateTime.Now)), CalendarPanel.PanelWidth);

        _dock.Changed += (_, _) =>
        {
            ExpandedContent.Width = ExpandedTargetWidth - 2;

            // Satır değişince açık ada yeni genişliğe kısa bir geçişle uyar. Genişlik aynıysa (ör. yalnız bir pencere
            // başlığı değişti) süren animasyon (açılmanın esnemesi) yeniden başlatılmaz.
            if (_hover.IsExpanded && !_closed && ExpandedTargetWidth != _widthTarget)
            {
                AnimateWidth(ExpandedTargetWidth, TimeSpan.FromSeconds(0.2), new CubicEase { EasingMode = EasingMode.EaseOut });
            }
        };

        PillBorder.ContextMenuOpening += (_, _) =>
        {
            HideTaskbarItem.IsChecked = _commands.IsTaskbarHidden();
            AssistantItem.IsChecked = _commands.IsAssistantEnabled();
            StartWithWindowsItem.IsChecked = _commands.IsStartWithWindows();
            HideRecordItem.IsChecked = _commands.IsRecordHidden();
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

            if (_hover.IsExpanded)
            {
                EndFlash(immediate: true); // genişleyen ada göstergeyi taşımaz
                EndScreenshot();
                HideAdaRow(); // panel ya da kısayol genişletti: oturum sürer, satır daralınca geri gelir
                Expanding?.Invoke(this, EventArgs.Empty);
            }

            AnimateTo(_hover.IsExpanded);
            if (!_hover.IsExpanded)
            {
                TryBeginAda();
            }

            ApplyPresence();
            UpdateMediaAnimation();
        });

        // Örtülü değilken hapın fare olayları, örtülüyken SmartHide'ın imleç yoklaması HoverIntent'i besler.
        PillBorder.MouseEnter += (_, _) => { if (!_smartHide.IsOccluded) _hover.PointerEntered(); };
        PillBorder.MouseLeave += (_, _) => { if (!_smartHide.IsOccluded) _hover.PointerExited(); };

        Closed += OnClosed;
        UpdateCompactView();

        RefreshClock();
    }

    /// <summary>Adanın hap şeklindeki gövdesi (genişleme ve cam görünümü bunu kullanır).</summary>
    public Border Pill => PillBorder;

    /// <summary>Genişleme niyeti; paneller açıkken <see cref="HoverIntent.SetPanelOpen"/> ile adayı büyük tutar.</summary>
    public HoverIntent Hover => _hover;

    /// <summary>Genişlemiş satır (Başlat, Ara, uygulamalar, ^ olayları için).</summary>
    public IslandBar Bar => ExpandedContent;

    /// <summary>Paneli klavyeyle açan kısayol (Win+Alt+V) için adayı fare beklemeden genişletir.</summary>
    public void ExpandForKeyboard() => _hover.ExpandNow();

    /// <summary>Ada genişlemeye başlayınca (ör. Wi‑Fi sinyal gücünü tazelemek için; Plan 4.3).</summary>
    public event EventHandler? Expanding;

    /// <summary>Küçük adada mikrofon kapalı işaretini gösterir ya da gizler.</summary>
    public void ShowMicOff(bool off)
    {
        CompactMicOff.Visibility = off ? Visibility.Visible : Visibility.Collapsed;
        UpdateDateText(DateTime.Now);
    }

    /// <summary>Küçük adada düşük pil uyarısı (Plan 4.3): "%18"; null gizler.</summary>
    public void ShowLowBattery(string? text)
    {
        CompactBatteryText.Text = text ?? "";
        CompactBattery.Visibility = text is null ? Visibility.Collapsed : Visibility.Visible;
        AutomationProperties.SetName(CompactBattery, text is null ? "" : $"Pil düşük: {text}");
        UpdateDateText(DateTime.Now);
    }

    /// <summary>
    /// Kısa gösterge (spec §7.5): küçük hapın içinde içerik yerine glif + çubuk + metin; son çağrıdan 1,5 sn sonra kaybolur.
    /// Çizgiye inmiş ada gösterge boyunca görünür, tam ekranda görünmez. Ada genişlemişken ya da panel açıkken bir şey
    /// yapmaz (panel ve satır zaten gösteriyor).
    /// </summary>
    public void Flash(FlashContent content)
    {
        if (_closed || _hover.IsExpanded || _panels.IsOpen || _flashKind == FlashKind.Interactive)
        {
            return;
        }

        FlashGlyph.Text = content.Glyph;
        FlashTrack.Visibility = content.Level is null ? Visibility.Collapsed : Visibility.Visible;
        FlashFill.Width = FlashTrack.Width * Math.Clamp(content.Level ?? 0, 0, 1);
        FlashText.Text = content.Text;
        AutomationProperties.SetName(FlashRow, content.Text);

        _flashTimer.Stop();
        _flashTimer.Start();
        if (_flashKind == FlashKind.None)
        {
            _flashKind = FlashKind.Passive;
            FadeFlash(show: true, immediate: false);
            ApplyPresence();
        }
    }

    private void EndFlash(bool immediate)
    {
        _flashTimer.Stop();
        if (_flashKind != FlashKind.Passive)
        {
            return;
        }

        _flashKind = FlashKind.None;
        FadeFlash(show: false, immediate);
        ApplyPresence();
    }

    /// <summary>
    /// Ekran görüntüsü önizlemesi (spec §7.5, Plan 4.2): hap büyür, küçük resim + Kopyala / Düzenle görünür. Örtülü yerde
    /// de tıklanabilir; önizleme boyunca imleç adayı genişletmez. Tam ekranda, ada genişlemişken ya da panel açıkken
    /// gösterilmez. Yeni ekran görüntüsü gelirse içerik değişir ve süre yenilenir.
    /// </summary>
    public void ShowScreenshot(ImageSource thumbnail, Func<Task<bool>> copy, Action edit)
    {
        if (_closed || _fullScreen || _hover.IsExpanded || _panels.IsOpen || _adaSuppressing)
        {
            return;
        }

        EndFlash(immediate: true);
        ShotThumb.ImageSource = thumbnail;
        ShotCopyText.Text = "Kopyala";
        _shotCopy = copy;
        _shotEdit = edit;
        _shotTimer.Stop();
        _shotTimer.Interval = ShotHold;
        if (!ShotRow.IsMouseOver)
        {
            _shotTimer.Start();
        }

        if (_flashKind == FlashKind.Interactive)
        {
            return;
        }

        _flashKind = FlashKind.Interactive;
        _hover.Suppress(true);
        ShotRow.Visibility = Visibility.Visible;
        AnimateShotSize(IslandLayout.ScreenshotPeek);
        FadeShot(show: true);
        ApplyPresence();
    }

    private void EndScreenshot()
    {
        _shotTimer.Stop();
        if (_flashKind != FlashKind.Interactive)
        {
            return;
        }

        _flashKind = FlashKind.None;
        _shotCopy = null;
        _shotEdit = null;
        if (!_hover.IsExpanded)
        {
            AnimateShotSize(IslandLayout.Compact); // genişleme varsa boyutu AnimateTo belirler
        }

        FadeShot(show: false);
        _hover.Suppress(false);
        ApplyPresence();
        UpdateMediaAnimation();
    }

    private void AnimateShotSize(PillSize size)
    {
        var duration = SystemParameters.ClientAreaAnimation ? TimeSpan.FromSeconds(0.18) : TimeSpan.Zero;
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        AnimateWidth(size.Width, duration, easing);
        PillBorder.BeginAnimation(HeightProperty, new DoubleAnimation(size.Height, duration) { EasingFunction = easing });
    }

    private void FadeShot(bool show)
    {
        var duration = SystemParameters.ClientAreaAnimation ? TimeSpan.FromSeconds(0.15) : TimeSpan.Zero;
        var fade = new DoubleAnimation(show ? 1 : 0, duration);
        if (!show)
        {
            fade.Completed += (_, _) =>
            {
                if (_flashKind != FlashKind.Interactive)
                {
                    ShotRow.Visibility = Visibility.Collapsed;
                }
            };
        }

        ShotRow.IsHitTestVisible = show;
        ShotRow.BeginAnimation(OpacityProperty, fade);
        CompactLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 0 : 1, duration));
    }

    private void ShotRow_MouseEnter(object sender, System.Windows.Input.MouseEventArgs e) => _shotTimer.Stop();

    private void ShotRow_MouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_flashKind == FlashKind.Interactive)
        {
            _shotTimer.Interval = TimeSpan.FromSeconds(1);
            _shotTimer.Start();
        }
    }

    private async void ShotCopy_Click(object sender, RoutedEventArgs e)
    {
        if (_shotCopy is not { } copy)
        {
            return;
        }

        var copied = await copy();
        if (_shotCopy == copy) // beklerken yeni ekran görüntüsü geldiyse onun düğmesine dokunma
        {
            ShotCopyText.Text = copied ? "Kopyalandı" : "Kopyalanamadı";
        }
    }

    private void ShotEdit_Click(object sender, RoutedEventArgs e)
    {
        var edit = _shotEdit;
        EndScreenshot();
        edit?.Invoke();
    }

    private void FadeFlash(bool show, bool immediate)
    {
        var duration = immediate || !SystemParameters.ClientAreaAnimation ? TimeSpan.Zero : TimeSpan.FromSeconds(0.15);
        FlashRow.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 1 : 0, duration));
        if (_adaShown)
        {
            AdaRow.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 0 : 1, duration));
        }
        else
        {
            CompactLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 0 : 1, duration));
        }
    }

    /// <summary>
    /// Ada'nın oturum satırı (Plan 5; ekran görüntüsü önizlemesinin genişleme düzeniyle): hap büyür, solda nokta ya da glif,
    /// ortada tek satır. Dinlerken nokta nabız atar; konuşurken ✦ ve Ada'nın son cümlesi; hatada ⚠ ve ileti. Satır
    /// açıkken ada fareyle genişlemez; ada zaten genişse satır daralınca açılır. Tam ekranda ada gizlidir (yalnız ses).
    /// </summary>
    public void ShowAda(AdaState state)
    {
        _ada = state;
        var micOpen = state is AdaState.Connecting or AdaState.Listening or AdaState.Speaking;
        CompactAdaDot.Visibility = micOpen ? Visibility.Visible : Visibility.Collapsed;
        if (state is AdaState.Idle)
        {
            EndAda();
            return;
        }

        string text;
        switch (state)
        {
            case AdaState.Listening { UserText: { Length: > 0 } said }:
                text = "“" + AdaText.LastSentence(said, 70) + "”";
                break;
            case AdaState.Speaking speaking:
                text = speaking.AdaText.Length > 0 ? AdaText.LastSentence(speaking.AdaText, 70) : "…";
                break;
            case AdaState.Failed failed:
                text = failed.Message;
                break;
            default:
                text = "Dinliyorum…";
                break;
        }

        AdaLine.Text = text;
        AutomationProperties.SetName(AdaRow, text);
        AdaDot.Visibility = micOpen ? Visibility.Visible : Visibility.Collapsed;
        switch (state)
        {
            case AdaState.Speaking:
                AdaGlyph.Text = "✦";
                AdaGlyph.FontFamily = FontFamily;
                AdaGlyph.Foreground = (Brush)FindResource("AccentBrush");
                AdaGlyph.Visibility = Visibility.Visible;
                break;
            case AdaState.Failed:
                AdaGlyph.Text = "\uE7BA";
                AdaGlyph.FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets");
                AdaGlyph.Foreground = (Brush)FindResource("WarningBrush");
                AdaGlyph.Visibility = Visibility.Visible;
                break;
            default:
                AdaGlyph.Visibility = Visibility.Collapsed;
                break;
        }

        AdaGlyph.Margin = micOpen ? new Thickness(10, 0, 0, 0) : new Thickness(0);
        SetAdaPulse(state is AdaState.Connecting or AdaState.Listening);
        TryBeginAda();
    }

    private void TryBeginAda()
    {
        if (_adaShown || _closed || _ada is AdaState.Idle)
        {
            return;
        }

        if (!_hover.IsExpanded)
        {
            EndFlash(immediate: true);
            EndScreenshot();
        }

        if (!_adaSuppressing)
        {
            _adaSuppressing = true;
            _hover.Suppress(true);
        }

        if (_hover.IsExpanded)
        {
            return; // imleç ayrılıp ada daralınca açılır
        }

        _adaShown = true;
        AdaRow.Visibility = Visibility.Visible;
        AnimateShotSize(IslandLayout.AssistantPeek);
        FadeAda(show: true);
        ApplyPresence();
    }

    /// <summary>Oturum bitti: satır kapanır, ada küçülür ve yeniden fareyle genişleyebilir.</summary>
    private void EndAda()
    {
        SetAdaPulse(false);
        HideAdaRow();
        if (_adaSuppressing)
        {
            _adaSuppressing = false;
            _hover.Suppress(false);
        }
    }

    private void HideAdaRow()
    {
        if (!_adaShown)
        {
            return;
        }

        _adaShown = false;
        if (!_hover.IsExpanded)
        {
            AnimateShotSize(IslandLayout.Compact);
        }

        FadeAda(show: false);
        ApplyPresence();
    }

    private void FadeAda(bool show)
    {
        var duration = SystemParameters.ClientAreaAnimation ? TimeSpan.FromSeconds(0.15) : TimeSpan.Zero;
        var fade = new DoubleAnimation(show ? 1 : 0, duration);
        if (!show)
        {
            fade.Completed += (_, _) =>
            {
                if (!_adaShown)
                {
                    AdaRow.Visibility = Visibility.Collapsed;
                }
            };
        }

        AdaRow.IsHitTestVisible = show;
        AdaRow.BeginAnimation(OpacityProperty, fade);
        if (_flashKind == FlashKind.None)
        {
            CompactLayer.BeginAnimation(OpacityProperty, new DoubleAnimation(show ? 0 : 1, duration));
        }
    }

    /// <summary>Dinlerken nokta hafifçe nabız atar (ortak 30 kare/sn saatiyle; durunca tam opak).</summary>
    private void SetAdaPulse(bool on)
    {
        if (on == _adaPulsing)
        {
            return;
        }

        _adaPulsing = on;
        _adaFrame ??= OnAdaFrame;
        if (on)
        {
            _adaPulseTime = TimeSpan.Zero;
            AnimationTicker.Add(_adaFrame);
        }
        else
        {
            AnimationTicker.Remove(_adaFrame);
            AdaDot.Opacity = 1;
            CompactAdaDot.Opacity = 1;
        }
    }

    private void OnAdaFrame(TimeSpan elapsed)
    {
        _adaPulseTime += elapsed;
        var phase = _adaPulseTime.TotalSeconds % 1.6 / 1.6;
        var opacity = 0.45 + 0.55 * (0.5 + 0.5 * Math.Cos(2 * Math.PI * phase));
        AdaDot.Opacity = opacity;
        CompactAdaDot.Opacity = opacity;
    }

    /// <summary>Müzik panelindeki beğen ve "Sıradaki" kaynağı (Spotify'a bağlanınca).</summary>
    public ISpotifyExtras? MusicExtras { get; set; }

    /// <summary>Müzik panelinin altına eklenen görünüm (Spotify'a bağlanma); her açılışta yeniden oluşturulur.</summary>
    public Func<FrameworkElement?>? MusicFooter { get; set; }

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

        var flash = _flashKind != FlashKind.None ? _flashKind : _adaShown ? FlashKind.Interactive : FlashKind.None;
        var presence = IslandPresenceRule.Decide(_fullScreen, _smartHide.IsOccluded, _hover.IsExpanded, flash);

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
        var clickThrough = tucked || presence == IslandPresence.Peek; // örtülü yerdeki kısa gösterge de tıklama geçirir
        PillBorder.IsHitTestVisible = !clickThrough;

        // Katmanlı pencerede Windows tıklamayı piksel piksel yönlendirir; çizginin pikselleri tıklamayı yutmasın diye
        // çizgi hâlinde pencerenin tamamı tıklama geçirir. Açılma, imleç yoklamasıyla olur.
        Native.SetExtendedStyle(new WindowInteropHelper(this).Handle, Native.WS_EX_TRANSPARENT, on: clickThrough);
        PillBorder.BeginAnimation(OpacityProperty, new DoubleAnimation(tucked ? 0 : 1, duration));
        TuckLine.BeginAnimation(OpacityProperty, new DoubleAnimation(tucked ? 1 : 0, duration));

        UpdateTint();
        UpdateMediaAnimation();
    }

    private void OnMediaChanged(object? sender, EventArgs e) => UpdateCompactView();

    /// <summary>
    /// Küçük hâlin içeriği (spec §7.1): çalarken mini plak + "Şarkı · Sanatçı" + ekolayzır, değilse tarih. Duraklamada
    /// geçiş payı (<see cref="CompactViewRule.Grace"/>) dolunca bir kez yeniden değerlendirilir.
    /// </summary>
    private void UpdateCompactView()
    {
        if (_closed)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var track = _media.Current;
        var playing = track?.State == PlaybackState.Playing;
        var view = CompactViewRule.Decide(track, _media.LastPlayingAt, now);

        if (track is not null)
        {
            CompactTitle.Text = track.Title;
            CompactArtist.Text = string.IsNullOrWhiteSpace(track.Artist) ? "" : " · " + track.Artist;
        }

        CompactRecord.Show(_media.Cover, playing);
        CompactEqualizer.IsPlaying = playing;
        ExpandedContent.ShowMusic(track, _media.Cover);

        if (view != _compactView)
        {
            _compactView = view;
            var music = view == CompactView.Music;
            var duration = SystemParameters.ClientAreaAnimation ? TimeSpan.FromSeconds(0.2) : TimeSpan.Zero;
            CompactMusic.BeginAnimation(OpacityProperty, new DoubleAnimation(music ? 1 : 0, duration));
            CompactEqualizer.BeginAnimation(OpacityProperty, new DoubleAnimation(music ? 1 : 0, duration));
            CompactDate.BeginAnimation(OpacityProperty, new DoubleAnimation(music ? 0 : 1, duration));
        }

        _graceTimer.Stop();
        if (view == CompactView.Music && !playing && _media.LastPlayingAt is { } last)
        {
            var remaining = last + CompactViewRule.Grace - now;
            if (remaining > TimeSpan.Zero)
            {
                _graceTimer.Interval = remaining + TimeSpan.FromMilliseconds(50);
                _graceTimer.Start();
            }
        }

        UpdateMediaAnimation();
    }

    /// <summary>Mini plak ve ekolayzır yalnız küçük ada ekranda açıkken ve müzik görünümündeyken kıpırdar (boşta %0 işlemci).</summary>
    private void UpdateMediaAnimation()
    {
        var active = IsVisible && !_fullScreen && !_smartHide.IsOccluded && !_hover.IsExpanded
                     && _flashKind != FlashKind.Interactive && _compactView == CompactView.Music;
        CompactRecord.CanAnimate = active;
        CompactEqualizer.CanAnimate = active;
        ExpandedContent.MusicCanAnimate = IsVisible && !_fullScreen && _hover.IsExpanded;
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
        // WS_EX_NOACTIVATE yalnız ön plana geçmeyi engeller: bizim bir penceremiz (panel, şerit) öndeyken tıklama adayı
        // yine etkinleştirirdi. Panel bu etkinleşmenin ortasında odak kaybından kapanınca etkinleşme yarıda kalıyor,
        // ön plan başka uygulamaya düşüyor ve Windows fare basışını adaya hiç iletmiyordu: panel açıkken adaya yapılan
        // tıklama kayboluyordu. Ada tıklamayla hiç etkinleşmez; paneller arası geçişi PanelHost yapar.
        if (msg == Native.WM_MOUSEACTIVATE)
        {
            handled = true;
            return Native.MA_NOACTIVATE;
        }

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
        _widthTarget = width;
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

    /// <summary>Boştaki adada tarihin yanında gösterilecek hava durumu (null: yalnız tarih).</summary>
    public void ShowWeather(WeatherNow? weather)
    {
        _weather = weather;
        UpdateDateText(DateTime.Now);
    }

    /// <summary>"Pzt, 23 Kasım · 12° Bulutlu"; sığmazsa durum adı düşer ("Pzt, 23 Kasım · 12°").</summary>
    private void UpdateDateText(DateTime now)
    {
        var date = ClockFormatter.Date(now);
        if (_weather is not { } weather)
        {
            CompactDate.Text = date;
            return;
        }

        var shortText = $"{date} · {WeatherText.Temperature(weather.TemperatureC)}";
        var description = WeatherText.Describe(weather.WeatherCode);
        var fullText = description.Length > 0 ? $"{shortText} {description}" : shortText;
        CompactDate.Text = Fits(fullText) ? fullText : shortText;
    }

    private bool Fits(string text)
    {
        const double sidePadding = 32, clockAndGap = 60;
        var formatted = new FormattedText(text, CultureInfo.GetCultureInfo("tr-TR"), FlowDirection.LeftToRight,
            new Typeface(CompactDate.FontFamily, CompactDate.FontStyle, CompactDate.FontWeight, CompactDate.FontStretch),
            CompactDate.FontSize, Brushes.White, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        return formatted.Width <= IslandLayout.Compact.Width - sidePadding - clockAndGap - IndicatorsWidth();
    }

    /// <summary>Sağ sütundaki görünür işaretlerin (mikrofon kapalı, düşük pil) kenar boşluklarıyla genişliği.</summary>
    private double IndicatorsWidth()
    {
        double width = 0;
        foreach (var indicator in new FrameworkElement[] { CompactMicOff, CompactBattery })
        {
            if (indicator.Visibility == Visibility.Visible)
            {
                indicator.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
                width += indicator.DesiredSize.Width; // DesiredSize kenar boşluğunu içerir
            }
        }

        return width;
    }

    private void RefreshClock()
    {
        var now = DateTime.Now;
        UpdateDateText(now);
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

    private void StartWithWindowsItem_Click(object sender, RoutedEventArgs e) =>
        _commands.SetStartWithWindows(StartWithWindowsItem.IsChecked);

    private void HideRecordItem_Click(object sender, RoutedEventArgs e) =>
        _commands.SetRecordHidden(HideRecordItem.IsChecked);

    private void ExitItem_Click(object sender, RoutedEventArgs e) => _commands.Exit();

    /// <summary>Ada satırına tıklandı (oturumu bitirmek için).</summary>
    public event EventHandler? AdaRowClicked;

    private void AdaRow_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_adaShown && _ada is not AdaState.Failed)
        {
            e.Handled = true;
            AdaRowClicked?.Invoke(this, EventArgs.Empty);
        }
    }

    private void AssistantItem_Click(object sender, RoutedEventArgs e) =>
        _commands.SetAssistantEnabled(AssistantItem.IsChecked);

    private void AssistantSettingsItem_Click(object sender, RoutedEventArgs e) => _commands.OpenAssistantPanel();

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
        SetAdaPulse(false);
        _panels.Close();
        _clockTimer.Stop();
        _graceTimer.Stop();
        _flashTimer.Stop();
        _media.Changed -= OnMediaChanged;
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
