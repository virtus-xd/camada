using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace CamAda.Panels;

/// <summary>
/// Hızlı ayarlar kutucuğu (Plan 4.3): solda glif + başlık + alt yazı (basınca <see cref="Toggled"/>), isteğe bağlı sağda ›
/// (basınca <see cref="DetailsRequested"/>; → tuşu da). Açıkken vurgu renginde. İşlem sürerken (<see cref="IsBusy"/>)
/// basışlar yok sayılır.
/// </summary>
public partial class QuickTile : UserControl
{
    private readonly DispatcherTimer _noteTimer;
    private string _subtitle = "";
    private string _title = "";
    private bool _isOn;
    private bool _isAvailable = true;

    public QuickTile()
    {
        InitializeComponent();
        Foreground = (Brush)FindResource("TextBrush");
        _noteTimer = new DispatcherTimer(TimeSpan.FromSeconds(3), DispatcherPriority.Normal, (_, _) => EndNote(), Dispatcher);
        _noteTimer.Stop();
    }

    public event EventHandler? Toggled;

    public event EventHandler? DetailsRequested;

    public string Glyph
    {
        get => GlyphText.Text;
        set => GlyphText.Text = value;
    }

    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            TitleText.Text = value;
            UpdateNames();
        }
    }

    public string Subtitle
    {
        get => _subtitle;
        set
        {
            _subtitle = value;
            if (!_noteTimer.IsEnabled)
            {
                SubtitleText.Text = value;
            }

            UpdateNames();
        }
    }

    public bool IsOn
    {
        get => _isOn;
        set
        {
            _isOn = value;
            Bd.Background = (Brush)FindResource(value ? "AccentBrush" : "PanelItemBrush");
            Foreground = (Brush)FindResource(value ? "OnAccentBrush" : "TextBrush");
        }
    }

    public bool IsBusy { get; set; }

    public bool HasDetails
    {
        get => DetailsPart.Visibility == Visibility.Visible;
        set => DetailsPart.Visibility = value ? Visibility.Visible : Visibility.Collapsed;
    }

    /// <summary>false: soluk ve tıklanmaz (ör. adaptör yok).</summary>
    public bool IsAvailable
    {
        get => _isAvailable;
        set
        {
            _isAvailable = value;
            Opacity = value ? 1 : 0.45;
            MainPart.IsEnabled = value;
            DetailsPart.IsEnabled = value;
        }
    }

    /// <summary>Alt yazının yerine 3 sn kısa bir not gösterir (ör. "Değiştirilemedi").</summary>
    public void ShowNote(string note)
    {
        SubtitleText.Text = note;
        _noteTimer.Stop();
        _noteTimer.Start();
        UpdateNames();
    }

    public void FocusMain() => MainPart.Focus();

    /// <summary>Liste sayfasından dönünce odak okta olsun (yoksa ana bölümde).</summary>
    public void FocusDetails()
    {
        if (HasDetails)
        {
            DetailsPart.Focus();
        }
        else
        {
            MainPart.Focus();
        }
    }

    private void EndNote()
    {
        _noteTimer.Stop();
        SubtitleText.Text = _subtitle;
        UpdateNames();
    }

    private void UpdateNames()
    {
        AutomationProperties.SetName(MainPart, $"{_title}: {SubtitleText.Text}");
        AutomationProperties.SetName(DetailsPart, $"{_title} listesi");
    }

    private void MainPart_Click(object sender, RoutedEventArgs e)
    {
        if (!IsBusy)
        {
            Toggled?.Invoke(this, EventArgs.Empty);
        }
    }

    private void DetailsPart_Click(object sender, RoutedEventArgs e) => DetailsRequested?.Invoke(this, EventArgs.Empty);

    private void MainPart_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Right && HasDetails)
        {
            e.Handled = true;
            DetailsRequested?.Invoke(this, EventArgs.Empty);
        }
    }
}
