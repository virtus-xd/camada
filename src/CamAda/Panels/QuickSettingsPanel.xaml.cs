using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using CamAda.Core.Bluetooth;
using CamAda.Core.Network;
using CamAda.Core.Power;
using CamAda.QuickSettings;
using Windows.Devices.Radios;

namespace CamAda.Panels;

/// <summary>
/// Hızlı ayarlar paneli (Plan 4.3): pil durumu; Wi‑Fi, Bluetooth, Odak, Koyu tema kutucukları; Ekran görüntüsü, Kilitle,
/// Uyku, Görev Yöneticisi. Servis olaylarına yalnız panel açıkken abone olur. Wi‑Fi ve Bluetooth listeleri aynı panelin
/// içinde ayrı bir sayfadır; Esc liste sayfasından ana sayfaya döner.
/// </summary>
public partial class QuickSettingsPanel : UserControl, IPanelEscape
{
    public const double PanelWidth = 380;

    /// <summary>Odak Windows'ta izinli uygulamalara kapalı (Görev 1 spike'ı): kutucuk Windows'un Odak sayfasını açar.</summary>
    private const string FocusSettings = "ms-settings:focus";

    private readonly QuickServices _services;
    private readonly Dictionary<string, (Slider Slider, TextBlock Percent)> _brightnessRows = new();
    private bool _updatingBrightness;

    public QuickSettingsPanel(QuickServices services)
    {
        InitializeComponent();
        _services = services;

        WifiTile.Glyph = QuickGlyphs.WifiFull;
        WifiTile.Title = "Wi‑Fi";
        WifiTile.HasDetails = services.Wifi is not null;
        WifiTile.DetailsRequested += (_, _) => ShowPage(QuickPage.Wifi);
        BluetoothTile.Glyph = QuickGlyphs.Bluetooth;
        BluetoothTile.Title = "Bluetooth";
        BluetoothTile.HasDetails = services.Bluetooth is not null;
        BluetoothTile.DetailsRequested += (_, _) => ShowPage(QuickPage.Bluetooth);
        FocusTile.Glyph = QuickGlyphs.Focus;
        FocusTile.Title = "Odak";
        FocusTile.Subtitle = "Ayarlarda aç";
        ThemeTile.Glyph = QuickGlyphs.Theme;
        ThemeTile.Title = "Koyu tema";

        Loaded += (_, _) =>
        {
            _services.Network.Changed += OnServiceChanged;
            _services.Battery.Changed += OnServiceChanged;
            _services.Theme.Changed += OnServiceChanged;
            if (_services.Radios is { } radios)
            {
                radios.Changed += OnServiceChanged;
                _ = radios.RefreshAsync();
            }

            if (_services.Bluetooth is { } bluetoothDevices)
            {
                bluetoothDevices.Start(); // kutucuktaki "Buds2 bağlı" için; liste sayfası ayrıca Start/Stop eder
                bluetoothDevices.Changed += OnServiceChanged;
            }

            if (_services.Brightness is { } brightness)
            {
                brightness.InternalChanged += OnBrightnessChanged;
                _ = LoadBrightnessAsync(brightness);
            }

            Refresh();
            Dispatcher.BeginInvoke(WifiTile.FocusMain, DispatcherPriority.Input);
        };
        Unloaded += (_, _) =>
        {
            _services.Network.Changed -= OnServiceChanged;
            _services.Battery.Changed -= OnServiceChanged;
            _services.Theme.Changed -= OnServiceChanged;
            if (_services.Radios is { } radios)
            {
                radios.Changed -= OnServiceChanged;
            }

            if (_services.Brightness is { } brightness)
            {
                brightness.InternalChanged -= OnBrightnessChanged;
            }

            if (_services.Bluetooth is { } bluetoothDevices)
            {
                bluetoothDevices.Changed -= OnServiceChanged;
                bluetoothDevices.Stop();
            }
        };
    }

    public enum QuickPage
    {
        Main,
        Wifi,
        Bluetooth,
    }

    public QuickPage Page { get; private set; } = QuickPage.Main;

    public void ShowPage(QuickPage page)
    {
        var previous = Page;
        Page = page;
        DetailPage.Content = page switch
        {
            QuickPage.Wifi when _services.Wifi is { } wifi => new WifiListView(_services, wifi, () => ShowPage(QuickPage.Main)),
            QuickPage.Bluetooth when _services.Bluetooth is { } bluetooth =>
                new BluetoothListView(_services, bluetooth, () => ShowPage(QuickPage.Main)),
            _ => null,
        };
        MainPage.Visibility = page == QuickPage.Main ? Visibility.Visible : Visibility.Collapsed;
        DetailPage.Visibility = page == QuickPage.Main ? Visibility.Collapsed : Visibility.Visible;
        Slide(page == QuickPage.Main ? MainPage : DetailPage, fromRight: page != QuickPage.Main);
        if (page == QuickPage.Main)
        {
            Refresh();
            var tile = previous == QuickPage.Bluetooth ? BluetoothTile : WifiTile;
            Dispatcher.BeginInvoke(tile.FocusDetails, DispatcherPriority.Input);
        }
    }

    /// <summary>Sayfa geçişi: 0,18 sn yatay kayma ve belirme; "Animasyonları azalt" açıksa anında.</summary>
    private static void Slide(FrameworkElement page, bool fromRight)
    {
        if (!SystemParameters.ClientAreaAnimation)
        {
            return;
        }

        var duration = TimeSpan.FromSeconds(0.18);
        var ease = new CubicEase { EasingMode = EasingMode.EaseOut };
        var shift = new TranslateTransform();
        page.RenderTransform = shift;
        shift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(fromRight ? 24 : -24, 0, duration) { EasingFunction = ease });
        page.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, duration) { EasingFunction = ease });
    }

    public bool HandleEscape()
    {
        if (Page == QuickPage.Main)
        {
            return false;
        }

        ShowPage(QuickPage.Main);
        return true;
    }

    private void OnServiceChanged(object? sender, EventArgs e) => Refresh();

    /// <summary>Her ekran için bir satır: ☀ + kaydırıcı + yüzde; birden fazla ekranda satırın üstünde ekran adı.</summary>
    private async Task LoadBrightnessAsync(BrightnessService brightness)
    {
        await brightness.RefreshAsync();
        if (!IsLoaded)
        {
            return;
        }

        BrightnessRows.Children.Clear();
        _brightnessRows.Clear();
        var displays = brightness.Displays;
        foreach (var display in displays)
        {
            if (displays.Count > 1)
            {
                BrightnessRows.Children.Add(new TextBlock
                {
                    Text = display.Name,
                    FontSize = 11.5,
                    FontWeight = FontWeights.Bold,
                    Foreground = (Brush)FindResource("TextSecondaryBrush"),
                    Margin = new Thickness(4, BrightnessRows.Children.Count == 0 ? 0 : 8, 0, 4),
                });
            }

            BrightnessRows.Children.Add(CreateBrightnessRow(brightness, display));
        }

        BrightnessRows.Visibility = displays.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private Grid CreateBrightnessRow(BrightnessService brightness, BrightnessDisplay display)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(48) });
        var glyph = new TextBlock
        {
            Text = QuickGlyphs.Brightness,
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 15,
            Margin = new Thickness(8, 0, 0, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        var slider = new Slider
        {
            Style = (Style)FindResource("PanelSlider"),
            Minimum = 0,
            Maximum = 100,
            SmallChange = 1,
            LargeChange = 10,
            Margin = new Thickness(16, 0, 8, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        AutomationProperties.SetName(slider, $"{display.Name} parlaklığı");
        var percent = new TextBlock
        {
            FontSize = 12.5,
            FontWeight = FontWeights.Bold,
            TextAlignment = TextAlignment.Right,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Typography.SetNumeralAlignment(percent, FontNumeralAlignment.Tabular);
        Grid.SetColumn(slider, 1);
        Grid.SetColumn(percent, 2);
        row.Children.Add(glyph);
        row.Children.Add(slider);
        row.Children.Add(percent);

        var id = display.Id;
        _updatingBrightness = true;
        slider.Value = display.Level;
        _updatingBrightness = false;
        percent.Text = $"%{display.Level}";
        slider.ValueChanged += (_, e) =>
        {
            var level = (int)Math.Round(e.NewValue);
            percent.Text = $"%{level}";
            if (!_updatingBrightness)
            {
                brightness.Set(id, level);
            }
        };

        _brightnessRows[id] = (slider, percent);
        return row;
    }

    /// <summary>
    /// Fn tuşları ya da Windows: kaydırıcı fareyle sürüklenmiyorsa yeni değeri gösterir (Review Focus 3). Klavye odağı
    /// kaydırıcıda kalsa da güncellenir; yoksa bir kez tıklanan kaydırıcı panel açık kaldıkça Fn tuşlarını izlemezdi.
    /// Kendi gönderdiğimiz değerin yankısı zaten kaydırıcıdadır.
    /// </summary>
    private void OnBrightnessChanged(object? sender, BrightnessChange change)
    {
        if (change.FromCamAda || !_brightnessRows.TryGetValue(BrightnessService.InternalId, out var row)
            || row.Slider.IsMouseCaptureWithin)
        {
            return;
        }

        _updatingBrightness = true;
        row.Slider.Value = change.Level;
        _updatingBrightness = false;
        row.Percent.Text = $"%{change.Level}";
    }

    private void Refresh()
    {
        var battery = _services.Battery.Current;
        var header = BatteryText.Header(battery, System.Globalization.CultureInfo.CurrentUICulture);
        BatteryHeader.Visibility = header is null ? Visibility.Collapsed : Visibility.Visible;
        BatteryHeaderText.Text = header ?? "";
        BatteryHeaderGlyph.Text = QuickGlyphs.Battery(BatteryText.Icon(battery));
        System.Windows.Automation.AutomationProperties.SetName(BatteryHeader,
            $"{BatteryText.AccessibleName(battery, System.Globalization.CultureInfo.CurrentUICulture)}. Pil ayarlarını aç");

        var radios = _services.Radios;
        WifiTile.IsAvailable = radios?.HasWifi == true;
        WifiTile.IsOn = radios?.WifiOn == true;
        WifiTile.Subtitle = NetworkIcons.WifiTileSubtitle(_services.Network.Current, System.Globalization.CultureInfo.CurrentUICulture);

        BluetoothTile.IsAvailable = radios?.HasBluetooth == true;
        BluetoothTile.IsOn = radios?.BluetoothOn == true;
        var connected = _services.Bluetooth?.Devices.Where(d => d.Connected).Select(d => d.Name).ToList() ?? [];
        BluetoothTile.Subtitle = BluetoothKinds.TileSubtitle(radios?.HasBluetooth == true, radios?.BluetoothOn == true, connected, System.Globalization.CultureInfo.CurrentUICulture);

        ThemeTile.IsOn = _services.Theme.IsDark;
        ThemeTile.Subtitle = _services.Theme.IsDark ? "Açık" : "Kapalı";
    }

    private async Task ToggleRadioAsync(QuickTile tile, RadioKind kind, bool currentlyOn)
    {
        if (_services.Radios is not { } radios)
        {
            return;
        }

        tile.IsBusy = true;
        var ok = await radios.SetAsync(kind, !currentlyOn);
        tile.IsBusy = false;
        if (!ok)
        {
            tile.ShowNote("Değiştirilemedi");
        }

        Refresh();
    }

    private async void WifiTile_Toggled(object? sender, EventArgs e) =>
        await ToggleRadioAsync(WifiTile, RadioKind.WiFi, _services.Radios?.WifiOn == true);

    private async void BluetoothTile_Toggled(object? sender, EventArgs e) =>
        await ToggleRadioAsync(BluetoothTile, RadioKind.Bluetooth, _services.Radios?.BluetoothOn == true);

    private void FocusTile_Toggled(object? sender, EventArgs e)
    {
        ClosePanel();
        _services.Launcher.Open(FocusSettings);
    }

    private async void ThemeTile_Toggled(object? sender, EventArgs e)
    {
        ThemeTile.IsBusy = true;
        try
        {
            await _services.Theme.SetDarkAsync(!_services.Theme.IsDark);
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException)
        {
            ThemeTile.ShowNote("Değiştirilemedi");
        }
        finally
        {
            ThemeTile.IsBusy = false;
        }

        Refresh();
    }

    private void BatteryHeader_Click(object sender, RoutedEventArgs e)
    {
        ClosePanel();
        _services.Launcher.Open("ms-settings:batterysaver");
    }

    private async void Screenshot_Click(object sender, RoutedEventArgs e)
    {
        ClosePanel();
        await Task.Delay(300); // panel görüntüye girmesin
        _services.Launcher.Screenshot();
    }

    private void Lock_Click(object sender, RoutedEventArgs e)
    {
        ClosePanel();
        _services.Launcher.Lock();
    }

    private void Sleep_Click(object sender, RoutedEventArgs e)
    {
        ClosePanel();
        _services.Launcher.Sleep();
    }

    private void TaskManager_Click(object sender, RoutedEventArgs e)
    {
        ClosePanel();
        _services.Launcher.TaskManager();
    }

    private void ClosePanel() => (Window.GetWindow(this) as PanelWindow)?.Dismiss();
}
