using System.Globalization;
using CamAda.Core.Text;

namespace CamAda.Core.Clipboard;

/// <summary>Pano satırındaki "ne zaman kopyalandı" etiketi: "şimdi", "2 dk", "3 sa", "dün", "12 Kas" (İngilizcede "now", "2 min"…).</summary>
public static class ClipAge
{
    public static string Format(DateTimeOffset copiedAt, DateTimeOffset now, CultureInfo ui)
    {
        var age = now - copiedAt;
        if (age < TimeSpan.FromMinutes(1))
        {
            return CoreText.Get(nameof(CoreStrings.ClipAge_Now), ui);
        }

        if (age < TimeSpan.FromHours(1))
        {
            return CoreText.Format(ui, nameof(CoreStrings.Duration_Minutes), (int)age.TotalMinutes);
        }

        var copiedDay = DateOnly.FromDateTime(copiedAt.ToOffset(now.Offset).DateTime);
        var today = DateOnly.FromDateTime(now.DateTime);
        if (copiedDay == today)
        {
            return CoreText.Format(ui, nameof(CoreStrings.Duration_Hours), (int)age.TotalHours);
        }

        return copiedDay == today.AddDays(-1)
            ? CoreText.Get(nameof(CoreStrings.ClipAge_Yesterday), ui)
            : copiedAt.ToOffset(now.Offset).ToString(CoreText.Get(nameof(CoreStrings.Format_ShortDate), ui), ui);
    }
}
