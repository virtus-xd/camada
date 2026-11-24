using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CamAda.Core.Bluetooth;
using CamAda.QuickSettings;
using Windows.Devices.Radios;

namespace CamAda.Panels;

/// <summary>
/// Hızlı ayarlar içindeki Bluetooth listesi (Plan 4.3, tasarım "Bluetooth listesi"): eşleşmiş cihazlar, bağlılar üstte;
/// ses cihazlarında Bağlan/Ayır, diğerlerinde yalnız durum. "Yeni cihaz ekle" Windows'un Bluetooth ayarlarını açar.
/// </summary>
public partial class BluetoothListView : UserControl
{
    private const string BluetoothSettings = "ms-settings:bluetooth";

    private readonly QuickServices _services;
    private readonly BluetoothService _bluetooth;
    private readonly Action _goBack;
    private readonly HashSet<string> _busy = [];
    private readonly Dictionary<string, string> _notes = new();

    public BluetoothListView(QuickServices services, BluetoothService bluetooth, Action goBack)
    {
        InitializeComponent();
        _services = services;
        _bluetooth = bluetooth;
        _goBack = goBack;

        Loaded += (_, _) =>
        {
            _bluetooth.Start();
            _bluetooth.Changed += OnChanged;
            if (_services.Radios is { } radios)
            {
                radios.Changed += OnChanged;
            }

            Show();
            Dispatcher.BeginInvoke(() => BackButton.Focus(), DispatcherPriority.Input);
        };
        Unloaded += (_, _) =>
        {
            _bluetooth.Changed -= OnChanged;
            if (_services.Radios is { } radios)
            {
                radios.Changed -= OnChanged;
            }

            _bluetooth.Stop();
        };
    }

    private bool RadioOn => _services.Radios?.BluetoothOn ?? true;

    private void OnChanged(object? sender, EventArgs e) => Show();

    private void Show()
    {
        var radios = _services.Radios;
        RadioButton.Visibility = radios?.HasBluetooth == true ? Visibility.Visible : Visibility.Collapsed;
        RadioButton.Content = RadioOn ? "Açık" : "Kapalı";
        RadioButton.Style = (Style)FindResource(RadioOn ? "PanelAccentButton" : "PanelButton");
        AutomationProperties.SetName(RadioButton, RadioOn ? "Bluetooth açık; kapat" : "Bluetooth kapalı; aç");

        Rows.Children.Clear();
        if (!RadioOn)
        {
            ShowStatus("Bluetooth kapalı");
            return;
        }

        var devices = _bluetooth.Devices;
        if (devices.Count == 0)
        {
            ShowStatus("Eşleşmiş cihaz yok");
            return;
        }

        StatusText.Visibility = Visibility.Collapsed;
        foreach (var device in devices)
        {
            Rows.Children.Add(CreateRow(device));
        }
    }

    private void ShowStatus(string text)
    {
        StatusText.Text = text;
        StatusText.Visibility = Visibility.Visible;
    }

    private FrameworkElement CreateRow(BluetoothEntry device)
    {
        var row = new DockPanel { Margin = new Thickness(0, 0, 0, 4) };
        var card = new Border
        {
            CornerRadius = new CornerRadius(16),
            Background = (Brush)FindResource("PanelItemBrush"),
            Padding = new Thickness(10, 8, 8, 8),
            Child = row,
        };

        var status = _notes.TryGetValue(device.Id, out var note) ? note : device.Connected ? "Bağlı" : "Bağlı değil";
        if (BluetoothKinds.CanConnect(device.Kind))
        {
            var action = _bluetooth.CanSendAudioCommands
                ? new Button
                {
                    Style = (Style)FindResource(device.Connected ? "PanelButton" : "PanelAccentButton"),
                    Content = device.Connected ? "Ayır" : "Bağlan",
                    Padding = new Thickness(12, 4, 12, 4),
                    IsEnabled = !_busy.Contains(device.Id),
                }
                : new Button { Style = (Style)FindResource("PanelButton"), Content = "Ayarlarda bağlan ›", Padding = new Thickness(12, 4, 12, 4) };
            AutomationProperties.SetName(action, $"{device.Name}: {action.Content}");
            action.Click += async (_, _) =>
            {
                if (!_bluetooth.CanSendAudioCommands)
                {
                    ClosePanelAndOpen(BluetoothSettings);
                    return;
                }

                await ToggleAsync(device);
            };
            DockPanel.SetDock(action, Dock.Right);
            row.Children.Add(action);
        }

        var glyph = new TextBlock
        {
            Text = GlyphFor(device.Kind),
            FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
            FontSize = 15,
            Margin = new Thickness(2, 0, 12, 0),
            VerticalAlignment = VerticalAlignment.Center,
        };
        DockPanel.SetDock(glyph, Dock.Left);
        row.Children.Add(glyph);

        var texts = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        texts.Children.Add(new TextBlock
        {
            Text = device.Name,
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
        });
        texts.Children.Add(new TextBlock
        {
            Text = status,
            FontSize = 11.5,
            Foreground = (Brush)FindResource(device.Connected ? "AccentBrush" : "TextSecondaryBrush"),
            TextWrapping = TextWrapping.Wrap,
        });
        row.Children.Add(texts);
        AutomationProperties.SetName(card, $"{device.Name}, {status}");
        return card;
    }

    private async Task ToggleAsync(BluetoothEntry device)
    {
        if (!_busy.Add(device.Id))
        {
            return; // Review Focus 5: işlem sürerken ikinci basış yok sayılır
        }

        var connecting = !device.Connected;
        _notes[device.Id] = connecting ? "Bağlanıyor…" : "Ayrılıyor…";
        Show();
        var ok = connecting ? await _bluetooth.ConnectAsync(device) : await _bluetooth.DisconnectAsync(device);
        _busy.Remove(device.Id);
        if (ok)
        {
            _notes.Remove(device.Id);
        }
        else
        {
            _notes[device.Id] = connecting ? "Bağlanamadı — cihaz açık ve yakında mı?" : "Ayrılamadı";
        }

        if (IsLoaded)
        {
            Show();
        }
    }

    private static string GlyphFor(BluetoothDeviceKind kind) => kind switch
    {
        BluetoothDeviceKind.Audio => "",
        BluetoothDeviceKind.Mouse => "",
        BluetoothDeviceKind.Keyboard => "",
        BluetoothDeviceKind.Gamepad => "",
        BluetoothDeviceKind.Phone => "",
        BluetoothDeviceKind.Computer => "",
        _ => QuickGlyphs.Bluetooth,
    };

    private async void RadioButton_Click(object sender, RoutedEventArgs e)
    {
        if (_services.Radios is not { } radios)
        {
            return;
        }

        RadioButton.IsEnabled = false;
        await radios.SetAsync(RadioKind.Bluetooth, !radios.BluetoothOn);
        RadioButton.IsEnabled = true;
        Show();
    }

    private void BackButton_Click(object sender, RoutedEventArgs e) => _goBack();

    private void AddButton_Click(object sender, RoutedEventArgs e) => ClosePanelAndOpen(BluetoothSettings);

    private void ClosePanelAndOpen(string uri)
    {
        (Window.GetWindow(this) as PanelWindow)?.Dismiss();
        _services.Launcher.Open(uri);
    }
}
