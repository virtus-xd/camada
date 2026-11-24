using System.Diagnostics;
using System.Net.Http;
using System.Windows.Threading;
using CamAda.Core.Settings;
using CamAda.Core.Weather;
using Microsoft.Win32;

namespace CamAda.Weather;

/// <summary>
/// Boştaki adanın hava durumu (spec §7.1, §10): anahtarsız Open-Meteo, konum ayardan. Koordinat yoksa şehir adı bir kez
/// aranıp ayara yazılır. 30 dakikada bir ve uykudan uyanınca yenilenir; hata olursa son değer (en fazla 3 saatlik) korunur.
/// </summary>
public sealed class WeatherService : IDisposable
{
    private static readonly TimeSpan RefreshEvery = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan MaxAge = TimeSpan.FromHours(3);
    private static readonly HttpClient Http = CreateClient();

    private readonly Func<WeatherLocation?> _location;
    private readonly Action<WeatherLocation> _saveResolved;
    private readonly DispatcherTimer _timer;
    private WeatherNow? _last;
    private DateTimeOffset _lastAt;
    private bool _refreshing;

    public WeatherService(Func<WeatherLocation?> location, Action<WeatherLocation> saveResolved, Dispatcher dispatcher)
    {
        _location = location;
        _saveResolved = saveResolved;
        _timer = new DispatcherTimer(RefreshEvery, DispatcherPriority.Background, async (_, _) => await RefreshAsync(), dispatcher);
        _timer.Stop();
    }

    /// <summary>Son başarılı ölçüm; konum yoksa ya da ölçüm 3 saatten eskiyse null.</summary>
    public WeatherNow? Current => _last is not null && DateTimeOffset.Now - _lastAt <= MaxAge ? _last : null;

    /// <summary>Hava durumu değişince (arayüz iş parçacığında).</summary>
    public event EventHandler? Changed;

    public async void Start()
    {
        SystemEvents.PowerModeChanged += OnPowerModeChanged;
        _timer.Start();
        await RefreshAsync();
    }

    public void Dispose()
    {
        _timer.Stop();
        SystemEvents.PowerModeChanged -= OnPowerModeChanged;
    }

    private async void OnPowerModeChanged(object? sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode == PowerModes.Resume)
        {
            await _timer.Dispatcher.InvokeAsync(RefreshAsync).Task.Unwrap();
        }
    }

    private async Task RefreshAsync()
    {
        if (_refreshing)
        {
            return;
        }

        _refreshing = true;
        try
        {
            if (_location() is not { } location)
            {
                Publish(null);
                return;
            }

            if (location.Latitude is not { } latitude || location.Longitude is not { } longitude)
            {
                var found = OpenMeteo.ParseGeocode(await Http.GetStringAsync(OpenMeteo.GeocodeUrl(location.Name)));
                if (found is null)
                {
                    Debug.WriteLine($"Hava durumu konumu bulunamadı: {location.Name}");
                    return;
                }

                (latitude, longitude) = (found.Value.Latitude, found.Value.Longitude);
                _saveResolved(location with { Latitude = latitude, Longitude = longitude });
            }

            if (OpenMeteo.ParseForecast(await Http.GetStringAsync(OpenMeteo.ForecastUrl(latitude, longitude))) is { } now)
            {
                _lastAt = DateTimeOffset.Now;
                Publish(now);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            Debug.WriteLine($"Hava durumu alınamadı: {ex.Message}"); // son değer korunur
            Changed?.Invoke(this, EventArgs.Empty);              // 3 saati geçtiyse gizlensin
        }
        finally
        {
            _refreshing = false;
        }
    }

    private void Publish(WeatherNow? weather)
    {
        _last = weather;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CamAda");
        return client;
    }
}
