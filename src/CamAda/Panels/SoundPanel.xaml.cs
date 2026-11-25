using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using CamAda.Audio;
using CamAda.Core.Audio;
using ManagedShell.Common.Helpers;
using ManagedShell.Common.Enums;

namespace CamAda.Panels;

/// <summary>
/// "Ses ve mikrofon" paneli (spec §7.4): ana ses, çıkış cihazı seçimi (Görev 3: uygulama bazında ses ve mikrofon).
/// Servis olaylarına yalnız panel açıkken abone olur. Sürüklenen kaydırıcı dışarıdan gelen değerle güncellenmez.
/// </summary>
public partial class SoundPanel : UserControl
{
    public const double PanelWidth = 360;

    private readonly AudioService _audio;
    private readonly DispatcherTimer _appsTimer;
    private readonly Dictionary<string, AppRow> _appRows = new();
    private bool _updating;

    public SoundPanel(AudioService audio)
    {
        InitializeComponent();
        _audio = audio;
        _appsTimer = new DispatcherTimer(TimeSpan.FromSeconds(1), DispatcherPriority.Background, (_, _) => ShowApps(), Dispatcher);
        _appsTimer.Stop();
        Loaded += (_, _) =>
        {
            _audio.MasterChanged += OnMasterChanged;
            _audio.DevicesChanged += OnDevicesChanged;
            _audio.MicChanged += OnMicChanged;
            ShowMaster(_audio.Volume, _audio.Muted);
            ShowOutputs();
            ShowApps();
            ShowMic();
            _appsTimer.Start();
        };
        Unloaded += (_, _) =>
        {
            _appsTimer.Stop();
            _audio.MasterChanged -= OnMasterChanged;
            _audio.DevicesChanged -= OnDevicesChanged;
            _audio.MicChanged -= OnMicChanged;
        };
    }

    private void OnMicChanged(object? sender, EventArgs e) => ShowMic();

    private void ShowMic()
    {
        var state = _audio.Mic;
        MicRow.Visibility = state == MicState.Missing ? Visibility.Collapsed : Visibility.Visible;
        NoMicText.Visibility = state == MicState.Missing ? Visibility.Visible : Visibility.Collapsed;
        if (state == MicState.Missing)
        {
            return;
        }

        var off = state == MicState.Off;
        MicGlyph.Text = off ? SpeakerGlyph.MicOff : SpeakerGlyph.MicOn;
        MicGlyph.Foreground = (Brush)FindResource(off ? "WarningBrush" : "TextBrush");
        MicNameText.Text = _audio.MicName ?? "Mikrofon";
        MicButton.Style = (Style)FindResource(off ? "PanelAccentButton" : "PanelButton");
        MicButton.Content = off ? "Mikrofonu aç" : "Mikrofonu kapat";
    }

    private void MicButton_Click(object sender, RoutedEventArgs e) => _audio.ToggleMic();

    /// <summary>
    /// Uygulama satırlarını anahtara göre yerinde günceller: yeni satır eklenir, kalkan silinir; sürüklenen ya da klavye
    /// odağındaki kaydırıcıya dokunulmaz (Review Focus 5).
    /// </summary>
    private void ShowApps()
    {
        var rows = _audio.Apps();
        foreach (var key in _appRows.Keys.Except(rows.Select(r => r.Key)).ToList())
        {
            AppList.Children.Remove(_appRows[key].Element);
            _appRows.Remove(key);
        }

        for (var i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            if (!_appRows.TryGetValue(row.Key, out var view))
            {
                view = CreateAppRow(row);
                _appRows[row.Key] = view;
                AppList.Children.Insert(Math.Min(i, AppList.Children.Count), view.Element);
            }

            view.Row = row;
            view.Name.Text = row.Name;

            // Windows'un karıştırıcısında sessize alınmış uygulama: satır soluk (kaydırıcı düzeyi yine gösterir).
            view.Element.Opacity = row.Muted ? 0.5 : 1;
            view.Element.ToolTip = row.Muted ? "Windows'ta sessize alınmış" : null;
            AutomationProperties.SetName(view.Slider, row.Muted ? $"{row.Name} ses düzeyi, sessiz" : $"{row.Name} ses düzeyi");
            if (!view.Slider.IsMouseCaptureWithin && !view.Slider.IsKeyboardFocusWithin)
            {
                _updating = true;
                view.Slider.Value = SpeakerLevels.Percent(row.Volume);
                _updating = false;
            }
        }

        NoAppsText.Visibility = rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    private AppRow CreateAppRow(AppVolumeRow row)
    {
        var grid = new Grid { Margin = new Thickness(4, 0, 0, 8) };
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(20) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(118) });
        grid.ColumnDefinitions.Add(new ColumnDefinition());

        FrameworkElement icon = _audio.ExePathFor(row) is { } path
            && IconImageConverter.GetImageFromAssociatedIcon(path, IconSize.Small) is { } image
            ? new Image { Source = image, Width = 20, Height = 20 }
            : new TextBlock
            {
                Text = "",
                FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                FontSize = 15,
                HorizontalAlignment = HorizontalAlignment.Center,
            };
        icon.VerticalAlignment = VerticalAlignment.Center;
        grid.Children.Add(icon);

        var name = new TextBlock
        {
            FontSize = 12.5,
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(10, 0, 8, 0),
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        };
        Grid.SetColumn(name, 1);
        grid.Children.Add(name);

        var slider = new Slider { Style = (Style)FindResource("PanelSlider"), VerticalAlignment = VerticalAlignment.Center };
        Grid.SetColumn(slider, 2);
        grid.Children.Add(slider);

        var view = new AppRow(grid, name, slider) { Row = row };
        slider.ValueChanged += (_, e) =>
        {
            if (!_updating)
            {
                _audio.SetAppVolume(view.Row, (float)(e.NewValue / 100.0));
            }
        };
        return view;
    }

    /// <summary>Bir uygulama satırının görünümü; <see cref="Row"/> her tazelemede güncellenir (süreç listesi değişebilir).</summary>
    private sealed class AppRow(FrameworkElement element, TextBlock name, Slider slider)
    {
        public FrameworkElement Element { get; } = element;
        public TextBlock Name { get; } = name;
        public Slider Slider { get; } = slider;
        public required AppVolumeRow Row { get; set; }
    }

    private void OnMasterChanged(object? sender, MasterVolumeChange change) => ShowMaster(change.Volume, change.Muted);

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        ShowMaster(_audio.Volume, _audio.Muted);
        ShowOutputs();
    }

    private void ShowMaster(float volume, bool muted)
    {
        var hasOutput = _audio.HasOutput;
        MuteButton.IsEnabled = hasOutput;
        VolumeSlider.IsEnabled = hasOutput;
        MuteGlyph.Text = SpeakerGlyph.For(SpeakerLevels.For(volume, muted || !hasOutput));
        AutomationProperties.SetName(MuteButton, muted ? "Sesi aç" : "Sesi kapat");
        PercentText.Text = muted ? "Sessiz" : $"%{SpeakerLevels.Percent(volume)}";

        if (!VolumeSlider.IsMouseCaptureWithin)
        {
            _updating = true;
            VolumeSlider.Value = SpeakerLevels.Percent(volume);
            _updating = false;
        }
    }

    private void ShowOutputs()
    {
        if (RowKeys.DeferWhilePressed(OutputList, ShowOutputs))
        {
            return;
        }

        var focused = RowKeys.Focused(OutputList);
        OutputList.Children.Clear();
        var outputs = _audio.Outputs();
        NoOutputText.Visibility = outputs.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

        foreach (var output in outputs)
        {
            var row = new Grid();
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                Text = output.Name,
                FontSize = 12.5,
                FontWeight = output.IsDefault ? FontWeights.Bold : FontWeights.SemiBold,
                TextTrimming = TextTrimming.CharacterEllipsis,
                VerticalAlignment = VerticalAlignment.Center,
            });
            if (output.IsDefault)
            {
                var check = new TextBlock
                {
                    Text = "",
                    FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                    FontSize = 13,
                    Foreground = (Brush)FindResource("AccentBrush"),
                    Margin = new Thickness(8, 0, 0, 0),
                    VerticalAlignment = VerticalAlignment.Center,
                };
                Grid.SetColumn(check, 1);
                row.Children.Add(check);
            }

            var button = new Button { Style = (Style)FindResource("PanelRowButton"), Content = row };
            AutomationProperties.SetName(button, output.IsDefault ? $"{output.Name}, seçili" : output.Name);
            var id = output.Id;
            RowKeys.SetKey(button, id);
            button.Click += (_, _) => _audio.SetDefaultOutput(id);
            OutputList.Children.Add(button);
        }

        RowKeys.Restore(OutputList, focused);
    }

    private void MuteButton_Click(object sender, RoutedEventArgs e) => _audio.SetMuted(!_audio.Muted);

    private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!_updating && IsLoaded)
        {
            _audio.SetVolume((float)(e.NewValue / 100.0));
        }
    }
}
