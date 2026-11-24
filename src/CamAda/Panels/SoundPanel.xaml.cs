using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using CamAda.Audio;
using CamAda.Core.Audio;

namespace CamAda.Panels;

/// <summary>
/// "Ses ve mikrofon" paneli (spec §7.4): ana ses, çıkış cihazı seçimi (Görev 3: uygulama bazında ses ve mikrofon).
/// Servis olaylarına yalnız panel açıkken abone olur. Sürüklenen kaydırıcı dışarıdan gelen değerle güncellenmez.
/// </summary>
public partial class SoundPanel : UserControl
{
    public const double PanelWidth = 360;

    private readonly AudioService _audio;
    private bool _updating;

    public SoundPanel(AudioService audio)
    {
        InitializeComponent();
        _audio = audio;
        Loaded += (_, _) =>
        {
            _audio.MasterChanged += OnMasterChanged;
            _audio.DevicesChanged += OnDevicesChanged;
            ShowMaster(_audio.Volume, _audio.Muted);
            ShowOutputs();
        };
        Unloaded += (_, _) =>
        {
            _audio.MasterChanged -= OnMasterChanged;
            _audio.DevicesChanged -= OnDevicesChanged;
        };
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
            button.Click += (_, _) => _audio.SetDefaultOutput(id);
            OutputList.Children.Add(button);
        }
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
