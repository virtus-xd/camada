using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using CamAda.Assistant;
using CamAda.Assistant.Audio;
using CamAda.Core.Audio;
using CamAda.Core.Assistant;
using CamAda.Core.Power;
using CamAda.Core.Settings;
using CamAda.Core.Text;

namespace CamAda.Panels;

/// <summary>Ada panelinin uygulamaya bağlandığı yerler.</summary>
/// <param name="ValidateKey">Anahtarla bir Live kurulumu dener (ses göndermeden); sorun yoksa <see cref="AssistantError.None"/>.</param>
/// <param name="SetEnabled">Ada'yı açar/kapatır (ayar, kısayol, dinleme).</param>
/// <param name="KeyChanged">Anahtar eklendi ya da silindi: dinleme yeniden değerlendirilir.</param>
/// <param name="Voiceprint">Kullanıcının ses izi (misafir kipi, Plan 8).</param>
/// <param name="ReserveMic">Tanıtma için mikrofonu Ada'dan alır; oturum sürüyorsa null.</param>
internal sealed record AdaPanelServices(
    GeminiKeyStore Keys,
    MemoryFile Memory,
    UsageFile Usage,
    Func<AssistantSettings> Settings,
    Action<AssistantSettings> SaveSettings,
    Func<IReadOnlyList<AudioOutput>> Microphones,
    Func<string, Task<AssistantError>> ValidateKey,
    Action<bool> SetEnabled,
    Action KeyChanged,
    VoiceprintFile Voiceprint,
    Func<IDisposable?> ReserveMic);

/// <summary>
/// Ada ayarları (tasarım "Anahtar ve ayarlar paneli"): anahtar yokken açıklama, AI Studio bağlantısı, gizli kutu ve
/// doğrulamalı Kaydet; anahtar varken aç/kapat, mikrofon seçimi, bu ayın tahmini harcaması, anahtarı silme ve hafıza
/// listesi (madde madde ×, iki adımlı "Hafızayı temizle").
/// </summary>
internal sealed partial class AdaPanel : UserControl
{
    public const double PanelWidth = 360;
    private const string StudioUrl = "https://aistudio.google.com/api-keys";
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(4);

    private readonly AdaPanelServices _services;
    private readonly ConfirmGate _forgetKeyGate = new(TimeProvider.System, ConfirmWindow);
    private readonly ConfirmGate _clearMemoryGate = new(TimeProvider.System, ConfirmWindow);
    private readonly ConfirmGate _deleteVoiceGate = new(TimeProvider.System, ConfirmWindow);
    private CancellationTokenSource? _enrolling;

    public AdaPanel(AdaPanelServices services)
    {
        InitializeComponent();
        _services = services;
        Loaded += (_, _) => _services.Memory.Changed += OnMemoryChanged;
        Unloaded += (_, _) =>
        {
            _services.Memory.Changed -= OnMemoryChanged;
            _enrolling?.Cancel(); // panel kapandı: tanıtma yarıda kalır, mikrofon bırakılır
        };
        Refresh();
    }

    private void Refresh()
    {
        var hasKey = _services.Keys.Load() is not null;
        KeySection.Visibility = hasKey ? Visibility.Collapsed : Visibility.Visible;
        SettingsSection.Visibility = hasKey ? Visibility.Visible : Visibility.Collapsed;
        if (!hasKey)
        {
            return;
        }

        var settings = _services.Settings();
        EnabledState.Text = settings.Enabled ? UiStrings.Common_On : UiStrings.Common_Off;
        EnabledState.Foreground = settings.Enabled ? (Brush)FindResource("AccentBrush") : (Brush)FindResource("TextSecondaryBrush");
        AutomationProperties.SetName(EnabledButton, settings.Enabled ? UiStrings.AdaPanel_EnabledLabel : UiStrings.AdaPanel_EnabledOff);
        ShowMicrophones(settings);
        ShowVoice();
        ShowUsage();
        ShowMemory();
    }

    // --- Sesim (misafir kipi, Plan 8) ---

    private void ShowVoice()
    {
        var status = _services.Voiceprint.Status(VoiceModels.SpeakerModel);
        VoiceStatus.Text = status switch
        {
            VoiceprintStatus.Ready when !OnCurrentMic() => UiStrings.Voice_OtherMic,
            VoiceprintStatus.Ready => UiStrings.Voice_Ready,
            VoiceprintStatus.Stale => UiStrings.Voice_Stale,
            _ => UiStrings.Voice_NotYet,
        };
        if (_enrolling is null)
        {
            EnrollButton.Content = status == VoiceprintStatus.Ready ? UiStrings.Voice_SetUpAgain : UiStrings.AdaPanel_EnrollVoice;
        }

        DeleteVoiceButton.Visibility = status == VoiceprintStatus.None ? Visibility.Collapsed : Visibility.Visible;
    }

    /// <summary>Ses izi şu anki Ada mikrofonuyla mı alındı (oturum başka mikrofonda izi kullanmaz).</summary>
    private bool OnCurrentMic()
    {
        if (_services.Voiceprint.Load(VoiceModels.SpeakerModel)?.Mic is not { } enrolledOn)
        {
            return true; // eski iz: mikrofonu bilinmiyor
        }

        try
        {
            return MicDevices.ResolveMic(_services.Settings().Microphone)?.ID == enrolledOn;
        }
        catch (System.Runtime.InteropServices.COMException)
        {
            return true;
        }
    }

    private async void Enroll_Click(object sender, RoutedEventArgs e)
    {
        if (_enrolling is { } running)
        {
            running.Cancel(); // "Vazgeç"
            return;
        }

        try
        {
            await EnrollAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            // Mikrofon ya da model beklenmedik biçimde düştü: uygulama düşmesin, kullanıcı yeniden denesin.
            Debug.WriteLine($"Ses tanıtılamadı: {ex.Message}");
            EnrollArea.Visibility = Visibility.Visible;
            EnrollStatus.Text = UiStrings.Voice_Failed;
        }
    }

    /// <summary>Beş cümle sırayla; kısık ya da kısa okunan cümle en çok üç kez denenir. Yalnız gömmeler kaydedilir.</summary>
    private async Task EnrollAsync()
    {
        using var reservation = _services.ReserveMic();
        EnrollArea.Visibility = Visibility.Visible;
        EnrollProgress.Text = "";
        EnrollSentence.Text = "";
        if (reservation is null)
        {
            EnrollStatus.Text = UiStrings.Voice_BusySpeaking;
            return;
        }

        using var cancel = new CancellationTokenSource();
        _enrolling = cancel;
        EnrollButton.Content = UiStrings.Common_Cancel;
        DeleteVoiceButton.IsEnabled = false;
        EnrollStatus.Text = UiStrings.Voice_MicOpening;
        try
        {
            using var enrollment = await VoiceEnrollment.OpenAsync(_services.Settings().Microphone, VoiceModels.Dir);
            if (enrollment is null)
            {
                EnrollStatus.Text = UiStrings.Voice_MicFailed;
                return;
            }

            var sentences = VoiceEnrollmentText.Sentences(CultureInfo.CurrentUICulture);
            var embeddings = new List<float[]>();
            for (var i = 0; i < sentences.Count; i++)
            {
                EnrollProgress.Text = $"{i + 1}/{sentences.Count}";
                EnrollSentence.Text = sentences[i];
                EnrollStatus.Text = UiStrings.Voice_ReadSentence;
                float[]? embedding = null;
                for (var attempt = 0; attempt < 3 && embedding is null; attempt++)
                {
                    var result = await enrollment.RecordSentenceAsync(cancel.Token);
                    embedding = result.Embedding;
                    EnrollStatus.Text = result.Check switch
                    {
                        SentenceCheck.TooQuiet => UiStrings.Voice_TooQuiet,
                        SentenceCheck.TooShort => UiStrings.Voice_TooShort,
                        _ => EnrollStatus.Text,
                    };
                }

                if (embedding is null)
                {
                    EnrollStatus.Text = UiStrings.Voice_Failed;
                    return;
                }

                embeddings.Add(embedding);
            }

            if (!_services.Voiceprint.Save(new Voiceprint(VoiceModels.SpeakerModel, embeddings, enrollment.MicId)))
            {
                EnrollStatus.Text = UiStrings.Voice_SaveFailed;
                return;
            }

            EnrollArea.Visibility = Visibility.Collapsed;
        }
        catch (OperationCanceledException)
        {
            EnrollArea.Visibility = Visibility.Collapsed;
        }
        finally
        {
            _enrolling = null;
            DeleteVoiceButton.IsEnabled = true;
            ShowVoice();
        }
    }

    private void DeleteVoice_Click(object sender, RoutedEventArgs e) => Confirm(DeleteVoiceButton, _deleteVoiceGate, UiStrings.Clip_Delete, () =>
    {
        _services.Voiceprint.Delete();
        EnrollArea.Visibility = Visibility.Collapsed;
        ShowVoice();
    });

    // --- Anahtar ---

    private void OpenStudio_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(StudioUrl) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Tarayıcı açılamadı: {ex.Message}");
        }
    }

    private void KeyBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            SaveKey_Click(sender, e);
        }
    }

    private async void SaveKey_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            await SaveKeyAsync();
        }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException or System.Security.Cryptography.CryptographicException)
        {
            // Anahtar dosyası yazılamadı: uygulama düşmesin, kullanıcı görsün.
            SaveKeyButton.IsEnabled = true;
            KeyStatus.Visibility = Visibility.Visible;
            KeyStatus.Text = string.Format(UiStrings.Key_SaveFailed, ex.Message);
        }
    }

    private async Task SaveKeyAsync()
    {
        var key = KeyBox.Password.Trim();
        if (key.Length == 0)
        {
            KeyBox.Focus();
            return;
        }

        SaveKeyButton.IsEnabled = false;
        KeyStatus.Visibility = Visibility.Visible;
        KeyStatus.Text = UiStrings.Key_Checking;
        var error = await _services.ValidateKey(key);
        SaveKeyButton.IsEnabled = true;
        if (error != AssistantError.None)
        {
            KeyStatus.Text = KeyMessage(error);
            return;
        }

        _services.Keys.Save(key);
        KeyBox.Clear();
        KeyStatus.Visibility = Visibility.Collapsed;
        _services.KeyChanged();
        Refresh();
        SavedNote.Text = UiStrings.Key_Verified;
        SavedNote.Visibility = Visibility.Visible;
    }

    private static string KeyMessage(AssistantError error) => error switch
    {
        AssistantError.InvalidKey => UiStrings.Key_Invalid,
        AssistantError.Network => UiStrings.Key_Network,
        AssistantError.PaymentRequired => UiStrings.Key_Payment,
        AssistantError.QuotaExceeded => UiStrings.Key_Quota,
        AssistantError.ModelUnavailable => UiStrings.Key_ModelUnavailable,
        _ => UiStrings.Key_CouldNotVerify,
    };

    private void ForgetKey_Click(object sender, RoutedEventArgs e) => Confirm(ForgetKeyButton, _forgetKeyGate, UiStrings.AdaPanel_DeleteKey, () =>
    {
        _services.SetEnabled(false);
        _services.Keys.Clear();
        _services.KeyChanged();
        SavedNote.Visibility = Visibility.Collapsed;
        Refresh();
    });

    // --- Aç/kapat ve mikrofon ---

    private void Enabled_Click(object sender, RoutedEventArgs e)
    {
        _services.SetEnabled(!_services.Settings().Enabled);
        SavedNote.Visibility = Visibility.Collapsed;
        Refresh();
    }

    /// <summary>"Windows varsayılanı" ve etkin kayıt cihazları; seçim ayara cihaz adıyla yazılır.</summary>
    private void ShowMicrophones(AssistantSettings settings)
    {
        MicList.Children.Clear();
        MicList.Children.Add(MicRow(UiStrings.Mic_WindowsDefault, settings.Microphone is null, null));
        foreach (var mic in _services.Microphones())
        {
            var selected = settings.Microphone is { } chosen && mic.Name.Contains(chosen, StringComparison.CurrentCultureIgnoreCase);
            MicList.Children.Add(MicRow(mic.Name, selected, mic.Name));
        }
    }

    private Button MicRow(string name, bool selected, string? value)
    {
        var row = new Grid();
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 12.5,
            FontWeight = selected ? FontWeights.Bold : FontWeights.SemiBold,
            TextTrimming = TextTrimming.CharacterEllipsis,
            VerticalAlignment = VerticalAlignment.Center,
        });
        if (selected)
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
        AutomationProperties.SetName(button, selected ? string.Format(UiStrings.Sound_Selected, name) : name);
        button.Click += (_, _) =>
        {
            _services.SaveSettings(_services.Settings() with { Microphone = value });
            Refresh();
        };
        return button;
    }

    private void ShowUsage()
    {
        UsageText.Text = _services.Usage.Current is { } month
            ? string.Format(CultureInfo.CurrentUICulture, UiStrings.Usage_ThisMonth, month.EstimatedUsd, month.Sessions, month.Searches)
            : UiStrings.Usage_None;
    }

    // --- Hafıza ---

    private void OnMemoryChanged() => Dispatcher.BeginInvoke(ShowMemory);

    private void ShowMemory()
    {
        MemoryList.Children.Clear();
        var items = _services.Memory.Snapshot();
        NoMemoryText.Visibility = items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ClearMemoryButton.Visibility = items.Count == 0 ? Visibility.Collapsed : Visibility.Visible;
        foreach (var item in items)
        {
            var row = new Grid { Margin = new Thickness(4, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition());
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.Children.Add(new TextBlock
            {
                FontSize = 12.5,
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center,
                Inlines =
                {
                    new System.Windows.Documents.Run(item.Topic + ": ") { FontWeight = FontWeights.Bold },
                    new System.Windows.Documents.Run(item.Fact),
                },
            });
            var remove = new Button
            {
                Style = (Style)FindResource("PanelButton"),
                Padding = new Thickness(8, 4, 8, 4),
                Margin = new Thickness(8, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Content = new TextBlock
                {
                    Text = "",
                    FontFamily = new FontFamily("Segoe Fluent Icons, Segoe MDL2 Assets"),
                    FontSize = 10,
                },
            };
            AutomationProperties.SetName(remove, string.Format(UiStrings.Memory_DeleteItem, item.Topic));
            var topic = item.Topic;
            remove.Click += (_, _) => _services.Memory.Forget(topic);
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);
            MemoryList.Children.Add(row);
        }
    }

    private void ClearMemory_Click(object sender, RoutedEventArgs e) =>
        Confirm(ClearMemoryButton, _clearMemoryGate, UiStrings.AdaPanel_ClearMemory, () => _services.Memory.Clear()); // yazılamazsa liste olduğu gibi kalır

    /// <summary>İki adımlı düğme: ilk basış "Emin misin?" der, süre içindeki ikinci basış işlemi yapar.</summary>
    private void Confirm(Button button, ConfirmGate gate, string label, Action action)
    {
        if (gate.Press())
        {
            button.Content = label;
            button.ClearValue(ForegroundProperty);
            action();
            return;
        }

        button.Content = UiStrings.Start_AreYouSure;
        button.Foreground = (Brush)FindResource("AccentBrush");
        var revert = new DispatcherTimer(DispatcherPriority.Normal, Dispatcher) { Interval = ConfirmWindow + TimeSpan.FromMilliseconds(50) };
        revert.Tick += (_, _) =>
        {
            revert.Stop();
            if (!gate.IsArmed)
            {
                button.Content = label;
                button.ClearValue(ForegroundProperty);
            }
        };
        revert.Start();
    }
}
