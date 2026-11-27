using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using CamAda.Core.Text;

namespace CamAda.Core.Assistant;

/// <summary>Bir hatırlatma; <see cref="Missed"/>: zamanında söylenemedi (uyku, CamAda kapalıydı).</summary>
public sealed record Reminder(Guid Id, string Text, DateTimeOffset At, bool Missed = false);

public abstract record ReminderChange
{
    private ReminderChange()
    {
    }

    public sealed record Added(Reminder Reminder) : ReminderChange;

    public sealed record Rejected(string Reason) : ReminderChange;
}

/// <summary>
/// Ada'nın hatırlatmaları (tasarım "Zamanlayıcı ve hatırlatıcı"): sınırlar, iptal, dolanlar ve erteleme. Dolanlar son 10 dk
/// "son dolanlar"da tutulur ("5 dakika ertele"). Saat <paramref name="time"/>'dan (duvar saati: uyku ve saat değişikliği kaçmaz).
/// </summary>
public sealed class ReminderBook(TimeProvider time)
{
    public const int MaxPending = 20;
    public const int MaxDays = 30;
    public const int MaxText = 100;
    private static readonly TimeSpan MissedAfter = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan SnoozeWindow = TimeSpan.FromMinutes(10);
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    private readonly List<Reminder> _pending = [];
    private readonly List<(Reminder Reminder, DateTimeOffset FiredAt)> _fired = [];

    /// <summary>Bekleyenler, yakından uzağa.</summary>
    public IReadOnlyList<Reminder> Pending => _pending.OrderBy(r => r.At).ToList();

    public event Action? Changed;

    public ReminderChange Add(string text, DateTimeOffset at)
    {
        var trimmed = text.Trim();
        var now = time.GetUtcNow();
        if (trimmed.Length == 0)
        {
            return new ReminderChange.Rejected("hatırlatma metni boş");
        }

        if (at <= now)
        {
            return new ReminderChange.Rejected("geçmiş bir zamana kurulamaz");
        }

        if (at - now > TimeSpan.FromDays(MaxDays))
        {
            return new ReminderChange.Rejected($"en çok {MaxDays} gün sonrasına kurulabilir");
        }

        if (_pending.Count >= MaxPending)
        {
            return new ReminderChange.Rejected($"en çok {MaxPending} bekleyen hatırlatma olabilir");
        }

        var reminder = new Reminder(Guid.NewGuid(), trimmed.Length > MaxText ? trimmed[..MaxText] : trimmed, at);
        _pending.Add(reminder);
        Changed?.Invoke();
        return new ReminderChange.Added(reminder);
    }

    /// <summary>
    /// Söylenen ada en çok benzeyeni siler (<see cref="NameMatch"/>; dizin <see cref="Pending"/> sırasına göre). "hepsi" hepsini
    /// siler ve <c>Found(-1)</c> döner. Belirsizse hiçbir şey silinmez.
    /// </summary>
    public NameMatchResult Cancel(string query)
    {
        if (query.Trim().ToLower(Turkish) == "hepsi")
        {
            _pending.Clear();
            Changed?.Invoke();
            return new NameMatchResult.Found(-1);
        }

        var ordered = Pending;
        var result = NameMatch.Best(query, ordered.Select(r => r.Text).ToList());
        if (result is NameMatchResult.Found found)
        {
            _pending.Remove(ordered[found.Index]);
            Changed?.Invoke();
        }

        return result;
    }

    /// <summary>Zamanı gelenleri çıkarır ve döner (yakından uzağa); 2 dk'dan geç fark edilenler "kaçırılan".</summary>
    public IReadOnlyList<Reminder> TakeDue()
    {
        var now = time.GetUtcNow();
        _fired.RemoveAll(f => now - f.FiredAt > SnoozeWindow);
        var due = _pending.Where(r => r.At <= now).OrderBy(r => r.At).ToList();
        if (due.Count == 0)
        {
            return [];
        }

        _pending.RemoveAll(r => r.At <= now);
        var result = due.Select(r => r with { Missed = now - r.At > MissedAfter }).ToList();
        _fired.AddRange(result.Select(r => (r, now)));
        Changed?.Invoke();
        return result;
    }

    /// <summary>Son 10 dk'da dolan en son hatırlatmayı <paramref name="minutes"/> sonra yeniden kurar.</summary>
    public ReminderChange Snooze(int minutes)
    {
        if (minutes is < 1 or > 1440)
        {
            return new ReminderChange.Rejected("dakika 1 ile 1440 arasında olmalı");
        }

        var now = time.GetUtcNow();
        _fired.RemoveAll(f => now - f.FiredAt > SnoozeWindow);
        if (_fired.Count == 0)
        {
            return new ReminderChange.Rejected("son 10 dakikada dolan bir hatırlatma yok");
        }

        var last = _fired.MaxBy(f => f.FiredAt);
        _fired.Remove(last);
        return Add(last.Reminder.Text, now.AddMinutes(minutes));
    }

    public string ToJson()
    {
        var pending = new JsonArray();
        foreach (var reminder in _pending)
        {
            pending.Add(new JsonObject
            {
                ["id"] = reminder.Id.ToString(),
                ["text"] = reminder.Text,
                ["at"] = reminder.At.ToString("O", CultureInfo.InvariantCulture),
            });
        }

        return new JsonObject { ["pending"] = pending }.ToJsonString();
    }

    /// <summary>Kayıtlı hatırlatmaları yükler; bozuk dosya ya da madde atlanır (boş defterle başlanır).</summary>
    public void LoadJson(string json)
    {
        _pending.Clear();
        _fired.Clear();
        try
        {
            if (JsonNode.Parse(json)?["pending"] is not JsonArray items)
            {
                return;
            }

            foreach (var item in items.OfType<JsonObject>())
            {
                if (Guid.TryParse(item["id"]?.GetValue<string>(), out var id)
                    && item["text"]?.GetValue<string>() is { Length: > 0 } text
                    && DateTimeOffset.TryParse(item["at"]?.GetValue<string>(), CultureInfo.InvariantCulture, DateTimeStyles.None, out var at))
                {
                    _pending.Add(new Reminder(id, text, at));
                }
            }
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException)
        {
            _pending.Clear();
        }
    }
}

/// <summary>Ada'nın verdiği süre ya da saatten hatırlatma zamanı (saati model değil uygulama hesaplar).</summary>
public static partial class ReminderTime
{
    /// <param name="nowLocal">Yerel "şimdi".</param>
    /// <param name="minutes">0 değilse şimdiden bu kadar sonra (1–1440).</param>
    /// <param name="clock">"HH:MM" ya da "HH.MM"; tarihsiz saat geçtiyse yarın.</param>
    /// <param name="date">"YYYY-MM-DD" ya da boş.</param>
    public static DateTimeOffset? Resolve(DateTimeOffset nowLocal, int minutes, string? clock, string? date, out string? error)
    {
        error = null;
        if (minutes != 0)
        {
            if (minutes is < 1 or > 1440)
            {
                error = "dakika 1 ile 1440 arasında olmalı";
                return null;
            }

            return nowLocal.AddMinutes(minutes);
        }

        if (string.IsNullOrWhiteSpace(clock))
        {
            error = "süre ya da saat gerekli";
            return null;
        }

        var match = ClockPattern().Match(clock.Trim());
        var hour = match.Success ? int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture) : -1;
        var minute = match.Success ? int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) : -1;
        if (hour is < 0 or > 23 || minute is < 0 or > 59)
        {
            error = "saat 'HH:MM' biçiminde olmalı";
            return null;
        }

        var time = new TimeOnly(hour, minute);
        DateOnly day;
        var dated = !string.IsNullOrWhiteSpace(date);
        if (dated)
        {
            if (!DateOnly.TryParseExact(date!.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out day))
            {
                error = "tarih 'YYYY-MM-DD' biçiminde olmalı";
                return null;
            }
        }
        else
        {
            day = DateOnly.FromDateTime(nowLocal.DateTime);
        }

        var at = new DateTimeOffset(day.ToDateTime(time), nowLocal.Offset);
        if (at <= nowLocal)
        {
            if (dated)
            {
                error = "geçmiş bir zamana kurulamaz";
                return null;
            }

            at = at.AddDays(1);
        }

        return at;
    }

    [GeneratedRegex(@"^(\d{1,2})[:.](\d{2})$")]
    private static partial Regex ClockPattern();
}

/// <summary>Hatırlatmaların Türkçe metinleri: Ada'nın söylediği zaman, adadaki geri sayım ve süre dolunca söylenen cümle.</summary>
public static class ReminderText
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");

    /// <summary>"14.41" (bugün), "yarın 09.00", "28 Kasım Cumartesi 09.00".</summary>
    public static string When(DateTimeOffset at, DateTimeOffset now)
    {
        var local = at.ToOffset(now.Offset);
        var clock = local.ToString("HH.mm", Turkish);
        var days = DateOnly.FromDateTime(local.DateTime).DayNumber - DateOnly.FromDateTime(now.DateTime).DayNumber;
        return days switch
        {
            0 => clock,
            1 => "yarın " + clock,
            _ => local.ToString("d MMMM dddd", Turkish) + " " + clock,
        };
    }

    /// <summary>Bir saatten yakınsa "9 dakika sonra (14.41)", değilse <see cref="When"/>.</summary>
    public static string Relative(DateTimeOffset at, DateTimeOffset now)
    {
        var left = at - now;
        if (left <= TimeSpan.Zero || left >= TimeSpan.FromHours(1))
        {
            return When(at, now);
        }

        return $"{(int)Math.Ceiling(left.TotalMinutes)} dakika sonra ({When(at, now)})";
    }

    /// <summary>Adadaki geri sayım: 60 dk altındaysa; 10 dk'dan fazlaysa "⏱ 43 dk", değilse "⏱ 9:41"; aksi hâlde null.</summary>
    public static string? Countdown(DateTimeOffset at, DateTimeOffset now, CultureInfo ui)
    {
        var left = at - now;
        if (left <= TimeSpan.Zero || left >= TimeSpan.FromHours(1))
        {
            return null;
        }

        if (left > TimeSpan.FromMinutes(10))
        {
            return "⏱ " + CoreText.Format(ui, nameof(CoreStrings.Duration_Minutes), (int)Math.Ceiling(left.TotalMinutes));
        }

        var seconds = (int)Math.Ceiling(left.TotalSeconds);
        return $"⏱ {seconds / 60}:{seconds % 60:00}";
    }

    /// <summary>
    /// Süre dolunca söylenen cümle: "Süre doldu: makarna ve çay." Kaçırılanlar ayrı cümle, saatleriyle: "Kaçırılan hatırlatma:
    /// toplantı, 09.00." ya da "Kaçırılan hatırlatmalar: toplantı (09.00) ve ilaç (10.00)."
    /// </summary>
    public static string Announcement(IReadOnlyList<Reminder> due, DateTimeOffset now)
    {
        var parts = new List<string>();
        var done = due.Where(r => !r.Missed).Select(r => r.Text).ToList();
        if (done.Count > 0)
        {
            parts.Add("Süre doldu: " + Join(done) + ".");
        }

        var missed = due.Where(r => r.Missed).ToList();
        if (missed.Count == 1)
        {
            parts.Add($"Kaçırılan hatırlatma: {missed[0].Text}, {When(missed[0].At, now)}.");
        }
        else if (missed.Count > 1)
        {
            parts.Add("Kaçırılan hatırlatmalar: " + Join(missed.Select(m => $"{m.Text} ({When(m.At, now)})").ToList()) + ".");
        }

        return string.Join(" ", parts);
    }

    private static string Join(IReadOnlyList<string> items) => items.Count switch
    {
        0 => "",
        1 => items[0],
        _ => string.Join(", ", items.Take(items.Count - 1)) + " ve " + items[^1],
    };
}
