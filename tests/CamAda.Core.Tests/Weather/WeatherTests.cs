using CamAda.Core.Settings;
using CamAda.Core.Weather;

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
        Assert.Equal(expected, WeatherText.Describe(code));

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
}
