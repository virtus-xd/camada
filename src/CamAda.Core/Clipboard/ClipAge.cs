using System.Globalization;

namespace CamAda.Core.Clipboard;

/// <summary>Pano satırındaki "ne zaman kopyalandı" etiketi: "şimdi", "2 dk", "3 sa", "dün", "12 Kas".</summary>
public static class ClipAge
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    public static string Format(DateTimeOffset copiedAt, DateTimeOffset now)
    {
        var age = now - copiedAt;
        if (age < TimeSpan.FromMinutes(1))
        {
            return "şimdi";
        }

        if (age < TimeSpan.FromHours(1))
        {
            return $"{(int)age.TotalMinutes} dk";
        }

        var copiedDay = DateOnly.FromDateTime(copiedAt.ToOffset(now.Offset).DateTime);
        var today = DateOnly.FromDateTime(now.DateTime);
        if (copiedDay == today)
        {
            return $"{(int)age.TotalHours} sa";
        }

        return copiedDay == today.AddDays(-1) ? "dün" : copiedAt.ToOffset(now.Offset).ToString("d MMM", Turkish);
    }
}
