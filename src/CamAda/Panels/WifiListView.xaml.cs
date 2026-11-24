using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CamAda.Core.Network;
using CamAda.QuickSettings;
using Windows.Devices.Radios;

namespace CamAda.Panels;

/// <summary>
/// Hızlı ayarlar içindeki Wi‑Fi listesi (Plan 4.3, tasarım "Wi‑Fi listesi"). Görünürken bir tarama yapar ve 10 sn'de bir
/// yeniler; bir satır açıkken (ör. şifre yazılırken) yenileme bekler. Sayfadan çıkınca tarama iptal edilir, gelen sonuç
/// yok sayılır; bağlanma ise sürer ama sonucu yalnız sayfa açıksa yazılır.
/// </summary>
public partial class WifiListView : UserControl
{
    private const string WifiSettings = "ms-settings:network-wifi";
    private static readonly TimeSpan RescanEvery = TimeSpan.FromSeconds(10);

    private readonly QuickServices _services;
    private readonly WifiService _wifi;
    private readonly Action _goBack;
    private readonly DispatcherTimer _rescan;
    private CancellationTokenSource? _scan;
    private IReadOnlyList<WifiRow> _rows = [];
    private bool _scanned;
    private string? _expanded;
    private bool _connecting;

    public WifiListView(QuickServices services, WifiService wifi, Action goBack)
    {
        InitializeComponent();
        _services = services;
        _wifi = wifi;
        _goBack = goBack;
        _rescan = new DispatcherTimer(RescanEvery, DispatcherPriority.Background, (_, _) => _ = ScanAsync(), Dispatcher);
        _rescan.Stop();

        Loaded += (_, _) =>
        {
            if (_services.Radios is { } radios)
            {
                radios.Changed += OnRadiosChanged;
            }

            UpdateHeader();
            _ = ScanAsync();
            _rescan.Start();
            Dispatcher.BeginInvoke(() => BackButton.Focus(), DispatcherPriority.Input);
        };
        Unloaded += (_, _) =>
        {
            _rescan.Stop();
            _scan?.Cancel();
            if (_services.Radios is { } radios)
            {
                radios.Changed -= OnRadiosChanged;
            }

            ClearPasswords();
        };
    }

    private bool RadioOn => _services.Radios?.WifiOn ?? true;

    private void OnRadiosChanged(object? sender, EventArgs e)
    {
        UpdateHeader();
        if (RadioOn)
        {
            _ = ScanAsync();
        }
        else
        {
            Show([]);
        }
    }

    private void UpdateHeader()
    {
        var radios = _services.Radios;
        RadioButton.Visibility = radios?.HasWifi == true ? Visibility.Visible : Visibility.Collapsed;
        RadioButton.Content = RadioOn ? "Açık" : "Kapalı";
        RadioButton.Style = (Style)FindResource(RadioOn ? "PanelAccentButton" : "PanelButton");
        AutomationProperties.SetName(RadioButton, RadioOn ? "Wi‑Fi açık; kapat" : "Wi‑Fi kapalı; aç");
    }

    private async Task ScanAsync()
    {
        if (!IsLoaded || _expanded is not null || !RadioOn)
        {
            if (!RadioOn)
            {
                Show([]);
            }

            return;
        }

        _scan?.Cancel();
        var scan = _scan = new CancellationTokenSource();
        if (!_scanned)
        {
            ShowStatus("Aranıyor…");
        }

        WifiScan result;
        try
        {
            result = await _wifi.ScanAsync(scan.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or COMException or UnauthorizedAccessException)
        {
            return;
        }

        if (scan.IsCancellationRequested || !IsLoaded || _expanded is not null)
        {
            return;
        }

        _scanned = true;
        LocationButton.Visibility = result.LocationBlocked ? Visibility.Visible : Visibility.Collapsed;
        Show(result.Rows, result.LocationBlocked);
    }

    private void Show(IReadOnlyList<WifiRow> rows, bool locationBlocked = false)
    {
        _rows = rows;
        Rows.Children.Clear();
        if (!RadioOn)
        {
            LocationButton.Visibility = Visibility.Collapsed;
            ShowStatus("Wi‑Fi kapalı");
            return;
        }

        if (locationBlocked)
        {
            ShowStatus("Ağ adlarını görmek için Windows konum izni gerekiyor.");
        }
        else if (rows.Count == 0)
        {
            ShowStatus(_scanned ? "Ağ bulunamadı" : "Aranıyor…");
        }
        else
        {
            StatusText.Visibility = Visibility.Collapsed;
        }

        foreach (var row in rows)
        {
            Rows.Children.Add(CreateRow(row));
        }
    }

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusText.Visibility = Visibility.Visible;
    }

    private FrameworkElement CreateRow(WifiRow row)
    {
        var right = row.Connected ? "Bağlı" : row.Security == WifiSecurity.Open ? "Açık ağ" : "";
        var header = new DockPanel();
        var rightPanel = new StackPanel { Orientation = Orientation.Horizontal };
        DockPanel.SetDock(rightPanel, Dock.Right);
        if (right.Length > 0)
        {
            rightPanel.Children.Add(new TextBlock
            {
                Text = right,
                FontSize = 11.5,
                FontWeight = FontWeights.SemiBold,
                Foreground = (Brush)FindResource(row.Connected ? "AccentBrush" : "TextSecondaryBrush"),
                VerticalAlignment = VerticalAlignment.Center,
            });
        }

        if (row.Security != WifiSecurity.Open && !row.Connected)
        {
            rightPanel.Children.Add(Glyph(QuickGlyphs.Lock, 12, secondary: true));
        }

        header.Children.Add(rightPanel);
        var bars = Glyph(QuickGlyphs.Bars(row.Bars), 15, secondary: false);
        bars.Margin = new Thickness(2, 0, 12, 0);
        DockPanel.SetDock(bars, Dock.Left);
        header.Children.Add(bars);
        header.Children.Add(new TextBlock
        {
            Text = row.Ssid,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });

        var button = new Button { Style = (Style)FindResource("PanelRowButton"), Content = header, Margin = new Thickness(0) };
        AutomationProperties.SetName(button, $"{row.Ssid}{(right.Length > 0 ? ", " + right : "")}");
        var container = new StackPanel { Margin = new Thickness(0, 0, 0, 4) };
        container.Children.Add(button);

        if (_expanded == row.Ssid)
        {
            container.Children.Add(CreateActions(row));
        }

        button.Click += (_, _) =>
        {
            if (row.Security == WifiSecurity.Enterprise && !row.Connected && !row.Saved)
            {
                ClosePanelAndOpen(WifiSettings);
                return;
            }

            _expanded = _expanded == row.Ssid ? null : row.Ssid;
            ClearPasswords();
            Show(_rows);
        };
        return container;
    }

    /// <summary>Açılan satırın altı: bağlı ağda "Bağlantıyı kes"; kayıtlı/şifresiz ağda "Bağlan"; yeni şifreli ağda şifre.</summary>
    private FrameworkElement CreateActions(WifiRow row)
    {
        var panel = new StackPanel { Margin = new Thickness(36, 8, 0, 6) };
        var status = new TextBlock
        {
            FontSize = 11.5,
            Margin = new Thickness(2, 0, 0, 6),
            Foreground = (Brush)FindResource("TextSecondaryBrush"),
            Visibility = Visibility.Collapsed,
        };
        panel.Children.Add(status);

        if (row.Connected)
        {
            var disconnect = new Button { Style = (Style)FindResource("PanelButton"), Content = "Bağlantıyı kes", HorizontalAlignment = HorizontalAlignment.Left };
            disconnect.Click += (_, _) =>
            {
                _wifi.Disconnect();
                _expanded = null;
                _ = Dispatcher.BeginInvoke(async () => await ScanAsync(), DispatcherPriority.Background);
            };
            panel.Children.Add(disconnect);
            Dispatcher.BeginInvoke(() => disconnect.Focus(), DispatcherPriority.Input);
            return panel;
        }

        var connect = new Button { Style = (Style)FindResource("PanelAccentButton"), Content = "Bağlan", HorizontalAlignment = HorizontalAlignment.Left };
        if (row.Saved || row.Security == WifiSecurity.Open)
        {
            connect.Click += async (_, _) => await ConnectAsync(row.Ssid, null, connect, status);
            panel.Children.Add(connect);
            Dispatcher.BeginInvoke(() => connect.Focus(), DispatcherPriority.Input);
            return panel;
        }

        // Yeni şifreli ağ: şifre kutusu + göster + Bağlan (Enter da bağlar).
        var hidden = new PasswordBox { Tag = "password" };
        var shown = new TextBox { Tag = "password", Visibility = Visibility.Collapsed };
        foreach (var box in new Control[] { hidden, shown })
        {
            box.FontSize = 13;
            box.Padding = new Thickness(8, 6, 8, 6);
            box.Background = Brushes.Transparent;
            box.Foreground = (Brush)FindResource("TextBrush");
            box.BorderThickness = new Thickness(0);
            AutomationProperties.SetName(box, $"{row.Ssid} şifresi");
        }

        hidden.CaretBrush = shown.CaretBrush = (Brush)FindResource("TextBrush");
        var reveal = new Button { Style = (Style)FindResource("PanelButton"), Padding = new Thickness(9, 6, 9, 6), Margin = new Thickness(6, 0, 0, 0) };
        reveal.Content = Glyph(QuickGlyphs.Reveal, 13, secondary: false);
        AutomationProperties.SetName(reveal, "Şifreyi göster");
        reveal.Click += (_, _) =>
        {
            var show = shown.Visibility != Visibility.Visible;
            if (show)
            {
                shown.Text = hidden.Password;
                hidden.Clear();
            }
            else
            {
                hidden.Password = shown.Text;
                shown.Clear();
            }

            shown.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
            hidden.Visibility = show ? Visibility.Collapsed : Visibility.Visible;
            AutomationProperties.SetName(reveal, show ? "Şifreyi gizle" : "Şifreyi göster");
        };

        string Password() => shown.Visibility == Visibility.Visible ? shown.Text : hidden.Password;
        async Task Submit()
        {
            var password = Password();
            if (password.Length == 0)
            {
                return;
            }

            await ConnectAsync(row.Ssid, password, connect, status);
        }

        connect.Click += async (_, _) => await Submit();
        KeyEventHandler onEnter = async (_, e) =>
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                await Submit();
            }
        };
        hidden.KeyDown += onEnter;
        shown.KeyDown += onEnter;

        var line = new DockPanel { Margin = new Thickness(0, 0, 0, 8) };
        DockPanel.SetDock(reveal, Dock.Right);
        line.Children.Add(reveal);
        line.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = (Brush)FindResource("PanelItemBrush"),
            Child = new Grid { Children = { hidden, shown } },
        });
        panel.Children.Add(line);
        panel.Children.Add(connect);
        Dispatcher.BeginInvoke(() => hidden.Focus(), DispatcherPriority.Input);
        return panel;
    }

    private async Task ConnectAsync(string ssid, string? password, Button connect, TextBlock status)
    {
        if (_connecting)
        {
            return; // Review Focus 5: ikinci basış yok sayılır
        }

        _connecting = true;
        connect.IsEnabled = false;
        status.Text = "Bağlanıyor…";
        status.Visibility = Visibility.Visible;
        WifiConnectOutcome outcome;
        try
        {
            outcome = await _wifi.ConnectAsync(ssid, password);
        }
        finally
        {
            _connecting = false;
            ClearPasswords();
        }

        if (!IsLoaded)
        {
            return;
        }

        connect.IsEnabled = true;
        status.Text = outcome switch
        {
            WifiConnectOutcome.Connected => "Bağlandı",
            WifiConnectOutcome.WrongPassword => "Şifre yanlış olabilir",
            _ => "Bağlanamadı",
        };

        if (outcome == WifiConnectOutcome.Connected)
        {
            await Task.Delay(1200);
            if (IsLoaded)
            {
                _expanded = null;
                await ScanAsync();
            }
        }
    }

    /// <summary>Açık satırdaki şifre kutularını boşaltır (şifre arayüzde kalmasın).</summary>
    private void ClearPasswords()
    {
        foreach (var box in FindPasswordBoxes(Rows))
        {
            switch (box)
            {
                case PasswordBox p:
                    p.Clear();
                    break;
                case TextBox t:
                    t.Clear();
                    break;
            }
        }
    }

    private static IEnumerable<Control> FindPasswordBoxes(DependencyObject root)
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(root); i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is Control { Tag: "password" } box)
            {
                yield return box;
            }

            foreach (var nested in FindPasswordBoxes(child))
            {
                yield return nested;
            }
        }
    }

    private TextBlock Glyph(string glyph, double size, bool secondary) => new()
    {
        Text = glyph,
        FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
        FontSize = size,
        Margin = new Thickness(8, 0, 0, 0),
        VerticalAlignment = VerticalAlignment.Center,
        Foreground = secondary ? (Brush)FindResource("TextSecondaryBrush") : (Brush)FindResource("TextBrush"),
    };

    private async void RadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (_services.Radios is not { } radios)
        {
            return;
        }

        RadioButton.IsEnabled = false;
        await radios.SetAsync(RadioKind.WiFi, !radios.WifiOn);
        RadioButton.IsEnabled = true;
        UpdateHeader();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _goBack();

    private void LocationButton_Click(object sender, RoutedEventArgs e) => ClosePanelAndOpen("ms-settings:privacy-location");

    private void MoreButton_Click(object sender, RoutedEventArgs e) => ClosePanelAndOpen(WifiSettings);

    private void ClosePanelAndOpen(string uri)
    {
        (Window.GetWindow(this) as PanelWindow)?.Dismiss();
        _services.Launcher.Open(uri);
    }
}
