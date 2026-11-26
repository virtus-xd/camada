using System.Globalization;
using System.Net.Http;
using CamAda.Core.Settings;
using CamAda.Core.Weather;

namespace CamAda.Weather;

/// <summary>
/// Ada'nın hava işlevi (Plan 9): Open-Meteo'dan anlık hava ve 7 günlük tahmin; şehir söylenmezse adanın kayıtlı şehri.
/// Ücretsiz ve anahtarsız (Google araması yerine). Yer başına 10 dk önbellek. "Bugün" yerin kendi saat dilimindeki ilk gündür.
/// </summary>
internal sealed class WeatherLookup(Func<WeatherLocation?> home)
{
    private static readonly HttpClient Http = CreateClient();
    private static readonly TimeSpan CacheFor = TimeSpan.FromMinutes(10);
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private const string Unreachable = "hata: hava durumuna ulaşılamadı";

    private readonly Dictionary<string, (WeatherReport Report, DateTimeOffset At)> _cache = [];

    public async Task<string> AnswerAsync(string? city, string? day, CancellationToken ct)
    {
        try
        {
            (string Place, double Latitude, double Longitude)? where;
            if (string.IsNullOrWhiteSpace(city))
            {
                if (home() is not { Name: { Length: > 0 } name } location)
                {
                    return "hata: kayıtlı şehir yok; hangi şehir?";
                }

                where = location.Coordinates is var (latitude, longitude) ? (name, latitude, longitude) : await GeocodeAsync(name, ct);
                if (where is null)
                {
                    return $"hata: '{name}' adlı bir yer bulunamadı";
                }
            }
            else
            {
                where = await GeocodeAsync(city.Trim(), ct);
                if (where is null)
                {
                    return $"hata: '{city.Trim()}' adlı bir yer bulunamadı";
                }
            }

            var (place, lat, lon) = where.Value;
            var key = string.Create(CultureInfo.InvariantCulture, $"{lat:F3},{lon:F3}");
            if (!_cache.TryGetValue(key, out var hit) || DateTimeOffset.UtcNow - hit.At > CacheFor)
            {
                if (OpenMeteo.ParseReport(await Http.GetStringAsync(OpenMeteo.DailyForecastUrl(lat, lon), ct)) is not { } report)
                {
                    return Unreachable;
                }

                hit = (report, DateTimeOffset.UtcNow);
                _cache[key] = hit;
            }

            return WeatherAnswer.For(place, hit.Report, day, hit.Report.Days[0].Date, Turkish);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return Unreachable;
        }
    }

    private static async Task<(string Place, double Latitude, double Longitude)?> GeocodeAsync(string name, CancellationToken ct) =>
        OpenMeteo.ParseGeocode(await Http.GetStringAsync(OpenMeteo.GeocodeUrl(name), ct)) is { } found
            ? (found.Name, found.Latitude, found.Longitude)
            : null;

    private static HttpClient CreateClient()
    {
        var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CamAda");
        return client;
    }
}
