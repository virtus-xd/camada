using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using CamAda.Core.Spotify;

namespace CamAda.Spotify;

/// <summary>
/// Müzik panelinin altındaki Spotify bağlantısı (spec §5, §7.4): Client ID yoksa "Beğen ve Sıradaki için Spotify'a bağlan"
/// ve üç adım (geliştirici panelini aç, yönlendirme adresini kopyala, Client ID'yi yapıştır); Client ID varsa yalnız
/// "Spotify'a bağlan". Bağlanınca <see cref="Connected"/> olur ve görünüm gizlenir. Kullanıcı kod okumaz: her şey buradan.
/// </summary>
public sealed partial class SpotifyConnectView : UserControl
{
    private const string DashboardUrl = "https://developer.spotify.com/dashboard";

    private readonly SpotifyClient _client;
    private readonly Action<string> _saveClientId;

    public SpotifyConnectView(SpotifyClient client, Action<string> saveClientId)
    {
        InitializeComponent();
        _client = client;
        _saveClientId = saveClientId;
        ShowSummary();
    }

    public event EventHandler? Connected;

    private void ShowSummary(string? note = null)
    {
        Visibility = _client.IsConnected && !_client.NeedsReconnect ? Visibility.Collapsed : Visibility.Visible;
        if (_client.NeedsReconnect)
        {
            // Bağlantı var ama Ada'nın çalma izni eklenmeden önce alınmış: aynı giriş akışı yeni izinle yeniden çalışır.
            SummaryText.Text = note ?? "Spotify izni yenilenmeli (Ada'nın çalabilmesi için).";
            StartButton.Content = "İzni yenile";
            StartButton.IsEnabled = true;
            return;
        }

        SummaryText.Text = note ?? "Beğen ve Sıradaki için Spotify'a bağlan.";
        StartButton.Content = _client.IsConfigured ? "Spotify'a bağlan" : "Bağla";
        StartButton.IsEnabled = true;
    }

    private async void StartButton_Click(object sender, RoutedEventArgs e)
    {
        if (_client.IsConfigured)
        {
            await ConnectAsync();
            return;
        }

        Steps.Visibility = Steps.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
    }

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        var clientId = ClientIdBox.Text.Trim();
        if (clientId.Length == 0)
        {
            ClientIdBox.Focus();
            return;
        }

        _saveClientId(clientId);
        Steps.Visibility = Visibility.Collapsed;
        await ConnectAsync();
    }

    private async Task ConnectAsync()
    {
        StartButton.IsEnabled = false;
        SummaryText.Text = "Tarayıcıda Spotify iznini onayla…";
        var ok = await _client.ConnectAsync(CancellationToken.None);
        if (ok)
        {
            Visibility = Visibility.Collapsed;
            Connected?.Invoke(this, EventArgs.Empty);
            return;
        }

        // Yanlış Client ID ya da eksik yönlendirme adresi olabilir: adımlar kayıtlı Client ID ile yeniden açılır.
        ShowSummary("Bağlanılamadı. Yönlendirme adresini ve Client ID'yi kontrol edip yeniden dene.");
        ClientIdBox.Text = _client.ClientId ?? "";
        Steps.Visibility = Visibility.Visible;
    }

    private void OpenDashboard_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Process.Start(new ProcessStartInfo(DashboardUrl) { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Tarayıcı açılamadı: {ex.Message}");
        }
    }

    private void CopyRedirect_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            Clipboard.SetText(SpotifyAuth.RedirectUri);
            CopyButton.Content = "Kopyalandı";
        }
        catch (COMException ex)
        {
            Debug.WriteLine($"Panoya kopyalanamadı: {ex.Message}");
        }
    }
}
