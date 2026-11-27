using System.Globalization;
using CamAda.Core.Text;

namespace CamAda.Core.Power;

public enum BatteryCharge
{
    NotPresent,
    Discharging,
    Charging,
    Idle,
}

/// <summary>Pil durumu; <paramref name="Remaining"/> Windows tahmin edemiyorsa null.</summary>
public sealed record BatterySnapshot(int Percent, BatteryCharge Charge, bool PluggedIn, TimeSpan? Remaining);

/// <summary>Pil simgesi: 0–10 doluluk, şarj, düşük pil uyarısı, prize takılı.</summary>
public sealed record BatteryIcon(int Level, bool Charging, bool Low, bool PluggedIn = false);

/// <summary>Adadaki pil simgesi, küçük ada uyarısı ve panel başlığındaki pil metni (Plan 4.3).</summary>
public static class BatteryText
{
    public const int LowPercent = 20;

    private static readonly TimeSpan Unknown = TimeSpan.FromDays(1);

    public static string? Header(BatterySnapshot s, CultureInfo ui)
    {
        var percent = CoreText.Percent(s.Percent, ui);
        return s.Charge switch
        {
            BatteryCharge.NotPresent => null,
            BatteryCharge.Charging => CoreText.Format(ui, nameof(CoreStrings.Battery_Charging), percent),
            BatteryCharge.Idle when s.PluggedIn && s.Percent >= 100 => CoreText.Get(nameof(CoreStrings.Battery_Full), ui),
            BatteryCharge.Idle when s.PluggedIn => CoreText.Format(ui, nameof(CoreStrings.Battery_PluggedIn), percent),
            _ => Remaining(s) is { } r ? $"{percent} · {FormatRemaining(r, ui)}" : percent,
        };
    }

    public static string AccessibleName(BatterySnapshot s, CultureInfo ui)
    {
        var percent = CoreText.Percent(s.Percent, ui);
        return s.Charge switch
        {
            BatteryCharge.NotPresent => CoreText.Get(nameof(CoreStrings.Battery_None), ui),
            BatteryCharge.Charging => CoreText.Format(ui, nameof(CoreStrings.Battery_A11yCharging), percent),
            BatteryCharge.Idle when s.PluggedIn && s.Percent >= 100 => CoreText.Format(ui, nameof(CoreStrings.Battery_A11yFull), percent),
            BatteryCharge.Idle when s.PluggedIn => CoreText.Format(ui, nameof(CoreStrings.Battery_A11yPluggedIn), percent),
            _ => Remaining(s) is { } r
                ? CoreText.Format(ui, nameof(CoreStrings.Battery_A11yRemaining), percent, FormatRemaining(r, ui))
                : CoreText.Format(ui, nameof(CoreStrings.Battery_A11y), percent),
        };
    }

    /// <summary>"3 sa 10 dk", "3 sa", "45 dk" (İngilizcede "3 h 10 min"); dakikalar aşağı yuvarlanır, en az 1 dakika.</summary>
    public static string FormatRemaining(TimeSpan t, CultureInfo ui)
    {
        var minutes = Math.Max(1, (int)t.TotalMinutes);
        var hours = minutes / 60;
        var rest = minutes % 60;
        return hours == 0 ? CoreText.Format(ui, nameof(CoreStrings.Duration_Minutes), rest)
            : rest == 0 ? CoreText.Format(ui, nameof(CoreStrings.Duration_Hours), hours)
            : CoreText.Format(ui, nameof(CoreStrings.Duration_HoursMinutes), hours, rest);
    }

    public static bool IsLow(BatterySnapshot s) => s.Charge == BatteryCharge.Discharging && s.Percent <= LowPercent;

    public static BatteryIcon Icon(BatterySnapshot s) =>
        new((int)Math.Clamp(Math.Round(s.Percent / 10.0, MidpointRounding.AwayFromZero), 0, 10),
            s.Charge == BatteryCharge.Charging, IsLow(s), s.PluggedIn);

    public static string CompactText(BatterySnapshot s, CultureInfo ui) => CoreText.Percent(s.Percent, ui);

    private static TimeSpan? Remaining(BatterySnapshot s) =>
        s.Remaining is { } r && r > TimeSpan.Zero && r < Unknown ? r : null;
}
