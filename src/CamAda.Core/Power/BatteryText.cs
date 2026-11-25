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

    public static string? Header(BatterySnapshot s) => s.Charge switch
    {
        BatteryCharge.NotPresent => null,
        BatteryCharge.Charging => $"Şarj oluyor · %{s.Percent}",
        BatteryCharge.Idle when s.PluggedIn && s.Percent >= 100 => "Tam dolu",
        BatteryCharge.Idle when s.PluggedIn => $"Prize takılı · %{s.Percent}",
        _ => Remaining(s) is { } r ? $"%{s.Percent} · {FormatRemaining(r)}" : $"%{s.Percent}",
    };

    public static string AccessibleName(BatterySnapshot s) => s.Charge switch
    {
        BatteryCharge.NotPresent => "Pil yok",
        BatteryCharge.Charging => $"Pil %{s.Percent}, şarj oluyor",
        BatteryCharge.Idle when s.PluggedIn && s.Percent >= 100 => $"Pil %{s.Percent}, tam dolu",
        BatteryCharge.Idle when s.PluggedIn => $"Pil %{s.Percent}, prize takılı",
        _ => Remaining(s) is { } r ? $"Pil %{s.Percent}, {FormatRemaining(r)} kaldı" : $"Pil %{s.Percent}",
    };

    /// <summary>"3 sa 10 dk", "3 sa", "45 dk"; dakikalar aşağı yuvarlanır, en az "1 dk".</summary>
    public static string FormatRemaining(TimeSpan t)
    {
        var minutes = Math.Max(1, (int)t.TotalMinutes);
        var hours = minutes / 60;
        var rest = minutes % 60;
        return hours == 0 ? $"{rest} dk" : rest == 0 ? $"{hours} sa" : $"{hours} sa {rest} dk";
    }

    public static bool IsLow(BatterySnapshot s) => s.Charge == BatteryCharge.Discharging && s.Percent <= LowPercent;

    public static BatteryIcon Icon(BatterySnapshot s) =>
        new((int)Math.Clamp(Math.Round(s.Percent / 10.0, MidpointRounding.AwayFromZero), 0, 10),
            s.Charge == BatteryCharge.Charging, IsLow(s), s.PluggedIn);

    public static string CompactText(BatterySnapshot s) => $"%{s.Percent}";

    private static TimeSpan? Remaining(BatterySnapshot s) =>
        s.Remaining is { } r && r > TimeSpan.Zero && r < Unknown ? r : null;
}
