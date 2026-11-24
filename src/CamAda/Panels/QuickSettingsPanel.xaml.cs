using System.Windows;
using System.Windows.Controls;
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

    public QuickSettingsPanel(QuickServices services)
    {
        InitializeComponent();
        _services = services;

        WifiTile.Glyph = QuickGlyphs.WifiFull;
        WifiTile.Title = "Wi‑Fi";
        BluetoothTile.Glyph = QuickGlyphs.Bluetooth;
        BluetoothTile.Title = "Bluetooth";
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
        Page = page;
        MainPage.Visibility = page == QuickPage.Main ? Visibility.Visible : Visibility.Collapsed;
        DetailPage.Visibility = page == QuickPage.Main ? Visibility.Collapsed : Visibility.Visible;
        if (page == QuickPage.Main)
        {
            DetailPage.Content = null;
            Refresh();
        }
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

    private void Refresh()
    {
        var battery = _services.Battery.Current;
        var header = BatteryText.Header(battery);
        BatteryHeader.Visibility = header is null ? Visibility.Collapsed : Visibility.Visible;
        BatteryHeaderText.Text = header ?? "";
        BatteryHeaderGlyph.Text = QuickGlyphs.Battery(BatteryText.Icon(battery));
        System.Windows.Automation.AutomationProperties.SetName(BatteryHeader,
            $"{BatteryText.AccessibleName(battery)}. Pil ayarlarını aç");

        var radios = _services.Radios;
        WifiTile.IsAvailable = radios?.HasWifi == true;
        WifiTile.IsOn = radios?.WifiOn == true;
        WifiTile.Subtitle = NetworkIcons.WifiTileSubtitle(_services.Network.Current);

        BluetoothTile.IsAvailable = radios?.HasBluetooth == true;
        BluetoothTile.IsOn = radios?.BluetoothOn == true;
        BluetoothTile.Subtitle = BluetoothKinds.TileSubtitle(radios?.HasBluetooth == true, radios?.BluetoothOn == true, []);

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
