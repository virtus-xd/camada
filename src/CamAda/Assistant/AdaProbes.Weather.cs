#if DEBUG
using System.Diagnostics;
using CamAda.Core.Settings;
using CamAda.Weather;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>Ada'nın hava işlevi (Plan 9 Görev 5): kayıtlı şehir, başka şehir, hafta, bulunamayan yer, aralık dışı gün.</summary>
    private static async Task WeatherTrialAsync()
    {
        var home = new SettingsStore(RoamingDir).Load().Weather;
        var lookup = new WeatherLookup(() => home);
        Log($"ada-hava-sina: kayıtlı şehir {home?.Name ?? "yok"}");
        foreach (var (city, day) in new (string?, string?)[] { (null, null), ("İzmir", "yarın"), (null, "hafta"), ("Böyleşehiryok", null), (null, "2027-01-01") })
        {
            var watch = Stopwatch.StartNew();
            var answer = await lookup.AnswerAsync(city, day, CancellationToken.None);
            Log($"hava ({city ?? "-"}, {day ?? "-"}) {watch.ElapsedMilliseconds} ms → {answer.ReplaceLineEndings(" | ")}");
        }
    }
}
#endif
