using System.Globalization;
using System.Text.Json;

namespace CamAda.Core.Weather;

/// <summary>Anlık hava: sıcaklık (°C) ve WMO hava kodu.</summary>
public sealed record WeatherNow(double TemperatureC, int WeatherCode);

/// <summary>Bir günün tahmini: hava kodu, en yüksek/en düşük (°C), en yüksek yağış olasılığı (%; yoksa null).</summary>
public sealed record WeatherDay(DateOnly Date, int Code, double MaxC, double MinC, int? RainChance);

/// <summary>Ada'nın hava yanıtı için anlık hava ve günlük tahmin (Plan 9).</summary>
public sealed record WeatherReport(WeatherNow Now, IReadOnlyList<WeatherDay> Days);

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

    /// <summary>Ada'nın hava yanıtı için: anlık hava ve 7 günlük tahmin, yerin saat diliminde.</summary>
    public static string DailyForecastUrl(double latitude, double longitude) => ForecastUrl(latitude, longitude) +
        "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=7";

    /// <summary>Anlık hava ve günler; anlık hava ya da geçerli tek gün yoksa (ya da yanıt bozuksa) null.</summary>
    public static WeatherReport? ParseReport(string json)
    {
        if (ParseForecast(json) is not { } now)
        {
            return null;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (!doc.RootElement.TryGetProperty("daily", out var daily) || daily.ValueKind != JsonValueKind.Object
                || !daily.TryGetProperty("time", out var times) || times.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            static JsonElement? At(JsonElement daily, string name, int i) =>
                daily.TryGetProperty(name, out var values) && values.ValueKind == JsonValueKind.Array && i < values.GetArrayLength()
                    ? values[i]
                    : null;

            var days = new List<WeatherDay>();
            for (var i = 0; i < times.GetArrayLength(); i++)
            {
                if (times[i].ValueKind == JsonValueKind.String
                    && DateOnly.TryParseExact(times[i].GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    && At(daily, "weather_code", i) is { ValueKind: JsonValueKind.Number } code && code.TryGetInt32(out var weatherCode)
                    && At(daily, "temperature_2m_max", i) is { ValueKind: JsonValueKind.Number } max && max.TryGetDouble(out var maxC)
                    && At(daily, "temperature_2m_min", i) is { ValueKind: JsonValueKind.Number } min && min.TryGetDouble(out var minC))
                {
                    int? rain = At(daily, "precipitation_probability_max", i) is { ValueKind: JsonValueKind.Number } r && r.TryGetInt32(out var chance) ? chance : null;
                    days.Add(new WeatherDay(date, weatherCode, maxC, minC, rain));
                }
            }

            return days.Count == 0 ? null : new WeatherReport(now, days);
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException)
        {
            return null;
        }
    }
}

/// <summary>
/// Ada'nın hava işlevinin sonucu (modele giden metin): anlık + bugün, yarın, bir tarih ya da 7 gün. Hava adları
/// <see cref="WeatherText.Describe"/>'dan, küçük harfle.
/// </summary>
public static class WeatherAnswer
{
    private const string OutOfRange = "hata: yalnız bugünden itibaren 7 günlük tahmin var";

    public static string For(string place, WeatherReport report, string? day, DateOnly today, CultureInfo turkish)
    {
        var key = day?.Trim().ToLower(turkish) ?? "";
        if (key == "hafta")
        {
            var lines = report.Days.Where(d => InRange(d.Date, today)).Select(d => $"{d.Date.ToString("d MMMM dddd", turkish)}: {DayText(d, turkish)}");
            return string.Join("\n", new[] { $"tamam: {place}, 7 gün:" }.Concat(lines));
        }

        DateOnly target;
        if (key is "" or "bugün" or "şimdi")
        {
            target = today;
        }
        else if (key == "yarın")
        {
            target = today.AddDays(1);
        }
        else if (!DateOnly.TryParseExact(key, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out target))
        {
            return OutOfRange;
        }

        var found = report.Days.FirstOrDefault(d => d.Date == target);
        if (!InRange(target, today) || (found is null && target != today))
        {
            return OutOfRange;
        }

        if (target == today)
        {
            var condition = Condition(report.Now.WeatherCode, turkish);
            var now = $"tamam: {place}, şimdi {WeatherText.Temperature(report.Now.TemperatureC)}" + (condition.Length > 0 ? ", " + condition : "");
            return found is null ? now : $"{now}; bugün {Numbers(found)}";
        }

        var label = target == today.AddDays(1) ? $"yarın ({target.ToString("d MMMM dddd", turkish)})" : target.ToString("d MMMM dddd", turkish);
        return $"tamam: {place}, {label}: {DayText(found!, turkish)}";
    }

    private static bool InRange(DateOnly date, DateOnly today) => date >= today && date < today.AddDays(7);

    private static string Condition(int code, CultureInfo turkish) => WeatherText.Describe(code).ToLower(turkish);

    private static string DayText(WeatherDay day, CultureInfo turkish) =>
        Condition(day.Code, turkish) is { Length: > 0 } condition ? $"{condition}, {Numbers(day)}" : Numbers(day);

    private static string Numbers(WeatherDay day) =>
        $"en yüksek {WeatherText.Temperature(day.MaxC)}, en düşük {WeatherText.Temperature(day.MinC)}"
        + (day.RainChance is { } rain ? $", yağış olasılığı %{rain}" : "");
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
