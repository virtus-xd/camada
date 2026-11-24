using System.Globalization;
using System.Text.Json;

namespace CamAda.Core.Weather;

/// <summary>Anlık hava: sıcaklık (°C) ve WMO hava kodu.</summary>
public sealed record WeatherNow(double TemperatureC, int WeatherCode);

/// <summary>Anahtarsız Open-Meteo servisi (spec §10): adresler ve yanıt ayrıştırma.</summary>
public static class OpenMeteo
{
    public static string ForecastUrl(double latitude, double longitude) => string.Create(CultureInfo.InvariantCulture,
        $"https://api.open-meteo.com/v1/forecast?latitude={latitude:0.####}&longitude={longitude:0.####}&current=temperature_2m,weather_code");

    public static string GeocodeUrl(string name) =>
        $"https://geocoding-api.open-meteo.com/v1/search?name={Uri.EscapeDataString(name)}&count=1&language=tr&format=json";

    /// <summary>current.temperature_2m ve current.weather_code; biri eksikse ya da yanıt bozuksa null.</summary>
    public static WeatherNow? ParseForecast(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("current", out var current)
                && current.ValueKind == JsonValueKind.Object
                && current.TryGetProperty("temperature_2m", out var temperature) && temperature.TryGetDouble(out var celsius)
                && current.TryGetProperty("weather_code", out var code) && code.TryGetInt32(out var weatherCode))
            {
                return new WeatherNow(celsius, weatherCode);
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) // bozuk ya da beklenmedik türde yanıt
        {
        }

        return null;
    }

    /// <summary>İlk sonucun koordinatları ve adı; sonuç yoksa ya da yanıt bozuksa null.</summary>
    public static (double Latitude, double Longitude, string Name)? ParseGeocode(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Object
                && doc.RootElement.TryGetProperty("results", out var results)
                && results.ValueKind == JsonValueKind.Array && results.GetArrayLength() > 0)
            {
                var first = results[0];
                if (first.ValueKind == JsonValueKind.Object
                    && first.TryGetProperty("latitude", out var lat) && lat.TryGetDouble(out var latitude)
                    && first.TryGetProperty("longitude", out var lon) && lon.TryGetDouble(out var longitude)
                    && first.TryGetProperty("name", out var name) && name.ValueKind == JsonValueKind.String
                    && name.GetString() is { } cityName)
                {
                    return (latitude, longitude, cityName);
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) // bozuk ya da beklenmedik türde yanıt
        {
        }

        return null;
    }
}

/// <summary>Adadaki hava durumu metinleri (Türkçe).</summary>
public static class WeatherText
{
    /// <summary>WMO hava kodunun kısa Türkçe adı; tanınmayan kod için boş metin.</summary>
    public static string Describe(int code) => code switch
    {
        0 => "Açık",
        1 or 2 => "Parçalı bulutlu",
        3 => "Bulutlu",
        45 or 48 => "Sisli",
        >= 51 and <= 57 => "Çisenti",
        >= 61 and <= 67 => "Yağmurlu",
        >= 71 and <= 77 => "Karlı",
        >= 80 and <= 82 => "Sağanak",
        85 or 86 => "Kar sağanağı",
        >= 95 and <= 99 => "Fırtına",
        _ => "",
    };

    /// <summary>Tam sayıya yuvarlanmış sıcaklık: "12°", "-3°"; eksi sıfır "0°" yazılır.</summary>
    public static string Temperature(double celsius)
    {
        var rounded = (int)Math.Round(celsius, MidpointRounding.AwayFromZero);
        return rounded.ToString(CultureInfo.InvariantCulture) + "°";
    }
}
