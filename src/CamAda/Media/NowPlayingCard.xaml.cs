using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CamAda.Core.Media;
using MediaTimeline = CamAda.Core.Media.MediaTimeline;

namespace CamAda.Media;

/// <summary>
/// Müzik paneli (spec §7.4) ve plak kartının (§7.8) ortak içeriği: kapak (istenirse), şarkı, "Sanatçı · Albüm", beğen,
/// ilerleme ve süreler, önceki / oynat-duraklat / sonraki, "Sıradaki". Parça yoksa "Şu an bir şey çalmıyor" ve
/// "Spotify'ı aç". Olaylara yüklüyken abone olur, kaldırılınca çıkar (kapanan panel bellekte kalmasın).
/// </summary>
public sealed partial class NowPlayingCard : UserControl
{
    private const string PlayGlyph = "";
    private const string PauseGlyph = "";
    private const string HeartGlyph = "";
    private const string HeartFilledGlyph = "";

    private static readonly Brush EmptyCover = Frozen(new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x22)));

    private readonly NowPlayingService _media;
    private readonly bool _showCover;
    private readonly int _queueCount;
    private readonly DispatcherTimer _progressTimer;
    private ISpotifyExtras? _extras;
    private ImageSource? _shownCover;
    private bool _loaded;

    public NowPlayingCard(NowPlayingService media, bool showCover, int queueCount)
    {
        InitializeComponent();
        _media = media;
        _showCover = showCover;
        _queueCount = queueCount;
        CoverFrame.Visibility = showCover ? Visibility.Visible : Visibility.Collapsed;
        _progressTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => UpdateProgress(), Dispatcher);
        _progressTimer.Stop();

        Loaded += (_, _) =>
        {
            _loaded = true;
            _media.Changed += OnMediaChanged;
            Subscribe(_extras);
            Update();
            _ = _extras?.RefreshAsync();
        };
        Unloaded += (_, _) =>
        {
            _loaded = false;
            _media.Changed -= OnMediaChanged;
            Unsubscribe(_extras);
            _progressTimer.Stop();
        };
        IsVisibleChanged += (_, _) =>
        {
            UpdateProgress(); // gizliyken ilerleme güncellenmez: görünür olunca hemen tazele
            UpdateProgressTimer();
        };
        ProgressTrack.SizeChanged += (_, _) => UpdateProgress(); // ilk yerleşimde çubuğun genişliği henüz 0
    }

    /// <summary>Beğen ve "Sıradaki" kaynağı (Görev 8); null iken ikisi de gizli.</summary>
    public ISpotifyExtras? Extras
    {
        get => _extras;
        set
        {
            if (_loaded)
            {
                Unsubscribe(_extras);
                Subscribe(value);
            }

            _extras = value;
            if (_loaded)
            {
                UpdateExtras();
                _ = value?.RefreshAsync();
            }
        }
    }

    private void Subscribe(ISpotifyExtras? extras)
    {
        if (extras is not null)
        {
            extras.Changed += OnExtrasChanged;
        }
    }

    private void Unsubscribe(ISpotifyExtras? extras)
    {
        if (extras is not null)
        {
            extras.Changed -= OnExtrasChanged;
        }
    }

    private void OnMediaChanged(object? sender, EventArgs e) => Update();

    private void OnExtrasChanged(object? sender, EventArgs e) => UpdateExtras();

    private void Update()
    {
        var track = _media.Current;
        PlayingView.Visibility = track is null ? Visibility.Collapsed : Visibility.Visible;
        EmptyView.Visibility = track is null ? Visibility.Visible : Visibility.Collapsed;
        if (track is null)
        {
            _progressTimer.Stop();
            return;
        }

        TitleText.Text = track.Title;
        SubtitleText.Text = string.Join(" · ", new[] { track.Artist, track.Album }.Where(s => !string.IsNullOrWhiteSpace(s)));

        if (_showCover && !ReferenceEquals(_shownCover, _media.Cover))
        {
            _shownCover = _media.Cover;
            CoverFrame.Background = _media.Cover is { } cover ? new ImageBrush(cover) { Stretch = Stretch.UniformToFill } : EmptyCover;
        }

        var playing = track.State == PlaybackState.Playing;
        PlayPauseButton.Content = playing ? PauseGlyph : PlayGlyph;
        AutomationProperties.SetName(PlayPauseButton, playing ? "Duraklat" : "Oynat");

        UpdateProgress();
        UpdateProgressTimer();
        UpdateExtras();
    }

    /// <summary>İlerleme çubuğu ve süreler; süre bilinmiyorsa ilerleme alanı gizlenir.</summary>
    private void UpdateProgress()
    {
        if (_media.Current is not { } track)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        var progress = MediaTimeline.Progress(track, now);
        ProgressArea.Visibility = progress is null ? Visibility.Collapsed : Visibility.Visible;
        if (progress is not { } fraction)
        {
            return;
        }

        ProgressFill.Width = Math.Max(0, ProgressTrack.ActualWidth * fraction);
        ElapsedText.Text = MediaTimeline.Format(MediaTimeline.PositionAt(track, now));
        DurationText.Text = MediaTimeline.Format(track.Duration);
    }

    /// <summary>İlerleme yalnız kart görünürken ve çalarken saniyede bir güncellenir.</summary>
    private void UpdateProgressTimer()
    {
        var run = _loaded && IsVisible && _media.Current?.State == PlaybackState.Playing;
        if (run)
        {
            _progressTimer.Start();
        }
        else
        {
            _progressTimer.Stop();
        }
    }

    private void UpdateExtras()
    {
        var available = _extras?.IsAvailable == true;
        if (available && _extras!.IsCurrentSaved is { } saved)
        {
            LikeButton.Visibility = Visibility.Visible;
            LikeButton.Content = saved ? HeartFilledGlyph : HeartGlyph;
            LikeButton.Foreground = saved ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("TextBrush");
            AutomationProperties.SetName(LikeButton, saved ? "Beğenmekten vazgeç" : "Beğen");
        }
        else
        {
            LikeButton.Visibility = Visibility.Collapsed;
        }

        var upNext = available ? _extras!.UpNext.Take(_queueCount).ToList() : [];
        UpNextList.ItemsSource = upNext;
        UpNextArea.Visibility = upNext.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void LikeButton_Click(object sender, RoutedEventArgs e)
    {
        if (_extras is not null)
        {
            await _extras.ToggleSavedAsync();
        }
    }

    private async void PlayPauseButton_Click(object sender, RoutedEventArgs e) => await _media.TogglePlayPauseAsync();

    private async void PreviousButton_Click(object sender, RoutedEventArgs e) => await _media.PreviousAsync();

    private async void NextButton_Click(object sender, RoutedEventArgs e) => await _media.NextAsync();

    private void OpenSpotifyButton_Click(object sender, RoutedEventArgs e) => _media.OpenSpotify();

    private static Brush Frozen(Brush brush)
    {
        brush.Freeze();
        return brush;
    }
}
