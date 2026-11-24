using System.Globalization;

namespace CamAda.Core.Calendar;

/// <summary>Takvim panelinde bir gün: ayın içinde mi, bugün mü.</summary>
public sealed record CalendarDay(DateOnly Date, bool InMonth, bool IsToday);

/// <summary>
/// Takvim panelinin ay görünümü (spec §7.4 "Takvim"): Pazartesi başlangıçlı, her zaman 6 hafta (42 gün) — panelin boyu aydan
/// aya değişmez.
/// </summary>
public static class MonthGrid
{
    public const int DayCount = 42;

    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>Pazartesiden pazara kısa gün adları.</summary>
    public static IReadOnlyList<string> WeekdayHeaders { get; } =
        Enumerable.Range(0, 7).Select(i => Turkish.DateTimeFormat.AbbreviatedDayNames[(i + 1) % 7]).ToArray();

    /// <summary>Ayın 1'ini içeren haftanın pazartesisinden başlayan 42 gün.</summary>
    public static IReadOnlyList<CalendarDay> Build(int year, int month, DateOnly today)
    {
        var first = new DateOnly(year, month, 1);
        var daysSinceMonday = ((int)first.DayOfWeek + 6) % 7;
        var start = first.AddDays(-daysSinceMonday);

        return Enumerable.Range(0, DayCount)
            .Select(i => start.AddDays(i))
            .Select(d => new CalendarDay(d, d.Year == year && d.Month == month, d == today))
            .ToArray();
    }

    /// <summary>Başlık, ör. "Kasım 2026".</summary>
    public static string Title(int year, int month) => new DateOnly(year, month, 1).ToString("MMMM yyyy", Turkish);

    /// <summary>Ay ileri/geri; yıl sınırını aşar.</summary>
    public static (int Year, int Month) Shift(int year, int month, int delta)
    {
        var shifted = new DateOnly(year, month, 1).AddMonths(delta);
        return (shifted.Year, shifted.Month);
    }
}
