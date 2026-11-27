using CamAda.Core.Settings;
using CamAda.Core.Weather;
using static CamAda.Core.Tests.Text.Cultures;

namespace CamAda.Core.Tests.Weather;

public class WeatherTests
{
    [Fact]
    public void Forecast_url_uses_invariant_decimals() =>
        Assert.Equal(
            "https://api.open-meteo.com/v1/forecast?latitude=41.0082&longitude=28.9784&current=temperature_2m,weather_code",
            OpenMeteo.ForecastUrl(41.00824, 28.97836));

    [Fact]
    public void Geocode_url_escapes_name() =>
        Assert.Equal(
            "https://geocoding-api.open-meteo.com/v1/search?name=%C4%B0zmir&count=1&language=tr&format=json",
            OpenMeteo.GeocodeUrl("İzmir"));

    [Fact]
    public void Forecast_is_parsed()
    {
        const string json = """
            {"latitude":39.92,"longitude":32.85,"current_units":{"temperature_2m":"°C"},
             "current":{"time":"2026-11-24T12:00","interval":900,"temperature_2m":12.4,"weather_code":3}}
            """;
        Assert.Equal(new WeatherNow(12.4, 3), OpenMeteo.ParseForecast(json));
    }

    [Fact]
    public void Forecast_without_fields_is_null() =>
        Assert.Null(OpenMeteo.ParseForecast("""{"current":{"temperature_2m":12.4}}"""));

    [Fact]
    public void Broken_forecast_is_null() =>
        Assert.Null(OpenMeteo.ParseForecast("<html>"));

    [Fact]
    public void Geocode_is_parsed()
    {
        const string json = """
            {"results":[{"id":323786,"name":"Ankara","latitude":39.91987,"longitude":32.85427,"country":"Türkiye"}],
             "generationtime_ms":0.5}
            """;
        Assert.Equal((39.91987, 32.85427, "Ankara"), OpenMeteo.ParseGeocode(json));
    }

    [Fact]
    public void Geocode_without_results_is_null() =>
        Assert.Null(OpenMeteo.ParseGeocode("""{"generationtime_ms":0.3}"""));

    [Theory]
    [InlineData(0, "Açık")]
    [InlineData(2, "Parçalı bulutlu")]
    [InlineData(3, "Bulutlu")]
    [InlineData(45, "Sisli")]
    [InlineData(53, "Çisenti")]
    [InlineData(63, "Yağmurlu")]
    [InlineData(75, "Karlı")]
    [InlineData(81, "Sağanak")]
    [InlineData(86, "Kar sağanağı")]
    [InlineData(96, "Fırtına")]
    [InlineData(4, "")]
    public void Weather_codes_are_described(int code, string expected) =>
        Assert.Equal(expected, WeatherText.Describe(code, Tr));

    [Theory]
    [InlineData(12.4, "12°")]
    [InlineData(-2.6, "-3°")]
    [InlineData(-0.2, "0°")]
    public void Temperature_is_rounded(double celsius, string expected) =>
        Assert.Equal(expected, WeatherText.Temperature(celsius));

    // Son inceleme: şehir adı değişip koordinatlar kalınca (Ankara → İzmir) sessizce eski şehrin havası gösteriliyordu.
    [Fact]
    public void Coordinates_are_used_only_for_the_name_they_were_found_for()
    {
        Assert.Equal((39.92, 32.85), new WeatherLocation("Ankara", 39.92, 32.85, "Ankara").Coordinates);
        Assert.Null(new WeatherLocation("İzmir", 39.92, 32.85, "Ankara").Coordinates);
        Assert.Null(new WeatherLocation("Ankara", 39.92, 32.85).Coordinates); // eski ayar: hangi ad için bulunduğu bilinmiyor
        Assert.Null(new WeatherLocation("Ankara", null, null, "Ankara").Coordinates);
    }

    [Fact]
    public void Unexpected_json_shapes_are_null()
    {
        Assert.Null(OpenMeteo.ParseForecast("""{"current":{"temperature_2m":"sıcak","weather_code":3}}"""));
        Assert.Null(OpenMeteo.ParseForecast("[1,2]"));
        Assert.Null(OpenMeteo.ParseGeocode("""{"results":[{"latitude":1,"longitude":2,"name":5}]}"""));
        Assert.Null(OpenMeteo.ParseGeocode("""{"results":[null]}"""));
        Assert.Null(OpenMeteo.ParseGeocode("[]"));
    }

    // --- Ada'nın hava durumu (Plan 9) ---

    private const string ReportJson = """
        {"current":{"temperature_2m":8.2,"weather_code":2},
         "daily":{"time":["2026-11-26","2026-11-27","2026-11-28"],"weather_code":[2,61,0],
                  "temperature_2m_max":[12.4,19.1,15],"temperature_2m_min":[3.2,10.6,4],
                  "precipitation_probability_max":[20,null,5]}}
        """;

    private static readonly DateOnly Today = new(2026, 11, 26);
    private static readonly System.Globalization.CultureInfo Turkish = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");

    private static WeatherReport Report() => OpenMeteo.ParseReport(ReportJson)!;

    [Fact]
    public void Daily_forecast_url_has_current_and_daily_fields() =>
        Assert.Equal(
            "https://api.open-meteo.com/v1/forecast?latitude=41.0082&longitude=28.9784&current=temperature_2m,weather_code" +
            "&daily=weather_code,temperature_2m_max,temperature_2m_min,precipitation_probability_max&timezone=auto&forecast_days=7",
            OpenMeteo.DailyForecastUrl(41.00824, 28.97836));

    [Fact]
    public void Report_is_parsed()
    {
        var report = Report();

        Assert.Equal(new WeatherNow(8.2, 2), report.Now);
        Assert.Equal(3, report.Days.Count);
        Assert.Equal(new WeatherDay(new DateOnly(2026, 11, 26), 2, 12.4, 3.2, 20), report.Days[0]);
        Assert.Null(report.Days[1].RainChance);
    }

    [Fact]
    public void Report_without_daily_is_null()
    {
        Assert.Null(OpenMeteo.ParseReport("""{"current":{"temperature_2m":8.2,"weather_code":2}}"""));
        Assert.Null(OpenMeteo.ParseReport("<html>"));
    }

    [Fact]
    public void Answer_for_now_and_today() =>
        Assert.Equal("tamam: Ankara, şimdi 8°, parçalı bulutlu; bugün en yüksek 12°, en düşük 3°, yağış olasılığı %20",
            WeatherAnswer.For("Ankara", Report(), null, Today, Turkish));

    [Fact]
    public void Answer_for_tomorrow_omits_missing_rain_chance()
    {
        const string expected = "tamam: İzmir, yarın (27 Kasım Cuma): yağmurlu, en yüksek 19°, en düşük 11°";
        Assert.Equal(expected, WeatherAnswer.For("İzmir", Report(), "yarın", Today, Turkish));
        Assert.Equal(expected, WeatherAnswer.For("İzmir", Report(), "2026-11-27", Today, Turkish));
    }

    [Fact]
    public void Answer_for_a_date() =>
        Assert.Equal("tamam: Ankara, 28 Kasım Cumartesi: açık, en yüksek 15°, en düşük 4°, yağış olasılığı %5",
            WeatherAnswer.For("Ankara", Report(), "2026-11-28", Today, Turkish));

    [Fact]
    public void Answer_for_the_week_has_a_line_per_day()
    {
        var lines = WeatherAnswer.For("Ankara", Report(), "hafta", Today, Turkish).Split('\n');

        Assert.Equal("tamam: Ankara, 7 gün:", lines[0]);
        Assert.Equal(4, lines.Length);
        Assert.Equal("26 Kasım Perşembe: parçalı bulutlu, en yüksek 12°, en düşük 3°, yağış olasılığı %20", lines[1]);
    }

    [Theory]
    [InlineData("2026-12-10")]
    [InlineData("2026-11-25")]
    [InlineData("pazartesi")]
    public void Answer_outside_the_forecast_is_an_error(string day) =>
        Assert.Equal("hata: yalnız bugünden itibaren 7 günlük tahmin var", WeatherAnswer.For("Ankara", Report(), day, Today, Turkish));
}
