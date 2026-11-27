using System.Globalization;

namespace CamAda.Core.Weather;

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

    private static string Condition(int code, CultureInfo turkish) => WeatherText.Describe(code, turkish).ToLower(turkish);

    private static string DayText(WeatherDay day, CultureInfo turkish) =>
        Condition(day.Code, turkish) is { Length: > 0 } condition ? $"{condition}, {Numbers(day)}" : Numbers(day);

    private static string Numbers(WeatherDay day) =>
        $"en yüksek {WeatherText.Temperature(day.MaxC)}, en düşük {WeatherText.Temperature(day.MinC)}"
        + (day.RainChance is { } rain ? $", yağış olasılığı %{rain}" : "");
}
