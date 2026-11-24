using System.Collections;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using CamAda.Audio;
using CamAda.Core.Audio;
using CamAda.Core.Media;

namespace CamAda.Island;

/// <summary>Genişlemiş adanın satırı: Başlat, Ara | uygulamalar | ^ | saat (spec §7.2).</summary>
public partial class IslandBar : UserControl
{
    public IslandBar()
    {
        InitializeComponent();
    }

    public event EventHandler? StartClicked;
    public event EventHandler? SearchClicked;
    public event EventHandler? TrayClicked;
    public event EventHandler? MusicClicked;
    public event EventHandler? ClockClicked;
    public event EventHandler? SoundClicked;
    public event EventHandler? ClipboardClicked;

    /// <summary>Ses düğmesinin hoparlör glifini ve mikrofon kapalı rozetini günceller.</summary>
    public void ShowSound(SpeakerLevel level, bool micMuted)
    {
        SoundGlyph.Text = SpeakerGlyph.For(level);
        MicOffBadge.Visibility = micMuted ? Visibility.Visible : Visibility.Collapsed;
        AutomationProperties.SetName(SoundButton, micMuted ? "Ses ve mikrofon: mikrofon kapalı" : "Ses ve mikrofon");
    }

    /// <summary>Müzik bölümünü günceller: parça varsa dönen mini plak ve şarkı adı, yoksa soluk nota ve "Spotify".</summary>
    public void ShowMusic(NowPlaying? track, ImageSource? cover)
    {
        var hasTrack = track is not null;
        MusicRecord.Visibility = hasTrack ? Visibility.Visible : Visibility.Collapsed;
        MusicGlyph.Visibility = hasTrack ? Visibility.Collapsed : Visibility.Visible;
        MusicTitle.Text = track?.Title ?? "Spotify";
        MusicTitle.Foreground = (Brush)FindResource(hasTrack ? "TextBrush" : "TextSecondaryBrush");
        MusicRecord.Show(cover, track?.State == PlaybackState.Playing);
        AutomationProperties.SetName(MusicButton, track is null
            ? "Müzik: çalmıyor"
            : string.IsNullOrWhiteSpace(track.Artist) ? $"Müzik: {track.Title}" : $"Müzik: {track.Title}, {track.Artist}");
    }

    /// <summary>Müzik bölümündeki mini plak yalnız genişlemiş ada ekrandayken döner.</summary>
    public bool MusicCanAnimate
    {
        get => MusicRecord.CanAnimate;
        set => MusicRecord.CanAnimate = value;
    }

    private void MusicButton_Click(object sender, RoutedEventArgs e) => MusicClicked?.Invoke(this, EventArgs.Empty);
    public event EventHandler<AppButtonEventArgs>? AppClicked;
    public event EventHandler<AppButtonEventArgs>? AppMiddleClicked;

    public IEnumerable? AppsSource
    {
        get => Apps.ItemsSource;
        set => Apps.ItemsSource = value;
    }

    public void SetTime(string time) => TimeText.Text = time;

    private void StartButton_Click(object sender, RoutedEventArgs e) => StartClicked?.Invoke(this, EventArgs.Empty);

    private void SearchButton_Click(object sender, RoutedEventArgs e) => SearchClicked?.Invoke(this, EventArgs.Empty);

    private void TrayButton_Click(object sender, RoutedEventArgs e) => TrayClicked?.Invoke(this, EventArgs.Empty);

    private void ClockButton_Click(object sender, RoutedEventArgs e) => ClockClicked?.Invoke(this, EventArgs.Empty);

    private void SoundButton_Click(object sender, RoutedEventArgs e) => SoundClicked?.Invoke(this, EventArgs.Empty);

    private void ClipboardButton_Click(object sender, RoutedEventArgs e) => ClipboardClicked?.Invoke(this, EventArgs.Empty);

    private void App_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: DockItemViewModel item } button)
        {
            AppClicked?.Invoke(this, new AppButtonEventArgs(item, button));
        }
    }

    private void App_PreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Middle && sender is Button { Tag: DockItemViewModel item } button)
        {
            AppMiddleClicked?.Invoke(this, new AppButtonEventArgs(item, button));
            e.Handled = true;
        }
    }
}

public sealed class AppButtonEventArgs(DockItemViewModel item, Button button) : EventArgs
{
    public DockItemViewModel Item { get; } = item;

    /// <summary>Tıklanan düğme (başlatma "zıplaması" ve panel konumu için).</summary>
    public Button Button { get; } = button;
}
