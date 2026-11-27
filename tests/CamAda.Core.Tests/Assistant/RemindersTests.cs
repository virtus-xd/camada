using CamAda.Core.Assistant;
using Microsoft.Extensions.Time.Testing;
using CamAda.Core.Tests.Text;

namespace CamAda.Core.Tests.Assistant;

public class RemindersTests
{
    private static readonly TimeSpan Tr = TimeSpan.FromHours(3);
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 26, 14, 32, 0, Tr)); // Perşembe

    public RemindersTests() => _time.SetLocalTimeZone(TimeZoneInfo.CreateCustomTimeZone("TR", Tr, "TR", "TR"));

    private DateTimeOffset Now => _time.GetUtcNow().ToOffset(Tr);

    private static DateTimeOffset At(int day, int hour, int minute, int second = 0) => new(2026, 11, day, hour, minute, second, Tr);

    private void Wait(double minutes) => _time.Advance(TimeSpan.FromMinutes(minutes));

    private DateTimeOffset? Resolve(int minutes, string? clock, string? date, out string? error) =>
        ReminderTime.Resolve(Now, minutes, clock, date, out error);

    // --- Saat çözümü ---

    [Fact]
    public void Minutes_from_now()
    {
        Assert.Equal(At(26, 14, 42), Resolve(10, null, null, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void Clock_today_if_future() => Assert.Equal(At(26, 17, 0), Resolve(0, "17:00", null, out _));

    [Fact]
    public void Clock_tomorrow_if_passed()
    {
        Assert.Equal(At(27, 9, 0), Resolve(0, "09:00", null, out _));
        Assert.Equal(At(27, 9, 30), Resolve(0, "9:30", "", out _));
    }

    [Fact]
    public void Clock_with_date() => Assert.Equal(At(28, 9, 0), Resolve(0, "09.00", "2026-11-28", out _));

    [Theory]
    [InlineData("25:00")]
    [InlineData("9")]
    [InlineData("21:5")]
    [InlineData("aa:bb")]
    public void Resolve_rejects_bad_clock(string clock)
    {
        Assert.Null(Resolve(0, clock, null, out var error));
        Assert.Equal("saat 'HH:MM' biçiminde olmalı", error);
    }

    [Fact]
    public void Resolve_rejects_past_date()
    {
        Assert.Null(Resolve(0, "10:00", "2026-11-25", out var error));
        Assert.Equal("geçmiş bir zamana kurulamaz", error);
        Assert.Null(Resolve(0, "10:00", "2026-11-26", out error));
        Assert.Equal("geçmiş bir zamana kurulamaz", error);
    }

    [Fact]
    public void Resolve_rejects_bad_date()
    {
        Assert.Null(Resolve(0, "10:00", "28.11.2026", out var error));
        Assert.Equal("tarih 'YYYY-MM-DD' biçiminde olmalı", error);
    }

    [Fact]
    public void Resolve_rejects_minutes_out_of_range()
    {
        Assert.Null(Resolve(1441, null, null, out var error));
        Assert.Equal("dakika 1 ile 1440 arasında olmalı", error);
        Assert.Null(Resolve(-5, null, null, out error));
        Assert.Equal("dakika 1 ile 1440 arasında olmalı", error);
        Assert.Null(Resolve(0, "", "", out error));
        Assert.Equal("süre ya da saat gerekli", error);
    }

    // --- Defter ---

    [Fact]
    public void Add_limits()
    {
        var book = new ReminderBook(_time);
        for (var i = 0; i < 20; i++)
        {
            Assert.IsType<ReminderChange.Added>(book.Add($"h{i}", Now.AddMinutes(i + 1)));
        }

        Assert.Equal(new ReminderChange.Rejected("en çok 20 bekleyen hatırlatma olabilir"), book.Add("fazla", Now.AddMinutes(30)));

        var other = new ReminderBook(_time);
        Assert.Equal(new ReminderChange.Rejected("en çok 30 gün sonrasına kurulabilir"), other.Add("uzak", Now.AddDays(31)));
        Assert.Equal(new ReminderChange.Rejected("hatırlatma metni boş"), other.Add("  ", Now.AddMinutes(5)));
        Assert.Equal(new ReminderChange.Rejected("geçmiş bir zamana kurulamaz"), other.Add("dün", Now.AddMinutes(-1)));
        var added = Assert.IsType<ReminderChange.Added>(other.Add("  " + new string('a', 150), Now.AddMinutes(5)));
        Assert.Equal(100, added.Reminder.Text.Length);
    }

    [Fact]
    public void Pending_is_ordered_by_time()
    {
        var book = new ReminderBook(_time);
        book.Add("geç", Now.AddMinutes(30));
        book.Add("erken", Now.AddMinutes(10));

        Assert.Equal(["erken", "geç"], book.Pending.Select(r => r.Text));
    }

    [Fact]
    public void Due_returns_all_due_once()
    {
        var book = new ReminderBook(_time);
        book.Add("makarna", Now.AddMinutes(10));
        book.Add("çay", Now.AddMinutes(10));
        book.Add("sonra", Now.AddMinutes(20));
        Wait(10);

        Assert.Equal(2, book.TakeDue().Count);
        Assert.Empty(book.TakeDue());
        Assert.Equal(["sonra"], book.Pending.Select(r => r.Text));
    }

    [Fact]
    public void Late_due_is_missed()
    {
        var book = new ReminderBook(_time);
        book.Add("geç kalan", Now.AddMinutes(10));
        book.Add("zamanında", Now.AddMinutes(12));
        Wait(13);

        var due = book.TakeDue();
        Assert.True(due.Single(r => r.Text == "geç kalan").Missed);
        Assert.False(due.Single(r => r.Text == "zamanında").Missed);
    }

    [Fact]
    public void Cancel_by_name_and_all()
    {
        var book = new ReminderBook(_time);
        book.Add("makarna", Now.AddMinutes(10));
        book.Add("toplantı", Now.AddMinutes(20));

        Assert.IsType<NameMatchResult.Found>(book.Cancel("Makarna"));
        Assert.Equal(["toplantı"], book.Pending.Select(r => r.Text));
        Assert.IsType<NameMatchResult.NotFound>(book.Cancel("yok"));

        book.Add("makas", Now.AddMinutes(5));
        book.Add("makarna", Now.AddMinutes(6));
        Assert.IsType<NameMatchResult.Ambiguous>(book.Cancel("mak"));
        Assert.Equal(3, book.Pending.Count); // belirsizken hiçbir şey silinmez

        Assert.Equal(new NameMatchResult.Found(-1), book.Cancel("hepsi"));
        Assert.Empty(book.Pending);
    }

    [Fact]
    public void Snooze_last_fired_within_ten_minutes()
    {
        var book = new ReminderBook(_time);
        book.Add("çay", Now.AddMinutes(1));
        Wait(1);
        book.TakeDue();
        Wait(5);

        var snoozed = Assert.IsType<ReminderChange.Added>(book.Snooze(5));
        Assert.Equal("çay", snoozed.Reminder.Text);
        Assert.Equal(Now.AddMinutes(5), snoozed.Reminder.At);

        Wait(5);
        book.TakeDue();
        Wait(11);
        Assert.Equal(new ReminderChange.Rejected("son 10 dakikada dolan bir hatırlatma yok"), book.Snooze(5));
        Assert.Equal(new ReminderChange.Rejected("dakika 1 ile 1440 arasında olmalı"), book.Snooze(0));
    }

    [Fact]
    public void Json_round_trips_and_FromJson_garbage_is_empty()
    {
        var book = new ReminderBook(_time);
        book.Add("makarna", Now.AddMinutes(10));
        book.Add("toplantı", At(27, 9, 0));

        var copy = new ReminderBook(_time);
        copy.LoadJson(book.ToJson());
        Assert.Equal(book.Pending, copy.Pending);

        var broken = new ReminderBook(_time);
        broken.LoadJson("{bozuk");
        Assert.Empty(broken.Pending);
        broken.LoadJson("""{"pending":[{"id":"x"}]}""");
        Assert.Empty(broken.Pending);
    }

    // --- Metinler ---

    [Fact]
    public void Countdown_formats()
    {
        Assert.Equal("⏱ 43 dk", ReminderText.Countdown(Now.AddMinutes(42).AddSeconds(10), Now, Cultures.Tr));
        Assert.Equal("⏱ 43 min", ReminderText.Countdown(Now.AddMinutes(42).AddSeconds(10), Now, Cultures.En));
        Assert.Equal("⏱ 9:41", ReminderText.Countdown(Now.AddMinutes(9).AddSeconds(41), Now, Cultures.Tr));
        Assert.Null(ReminderText.Countdown(Now.AddMinutes(61), Now, Cultures.Tr));
        Assert.Null(ReminderText.Countdown(Now.AddSeconds(-1), Now, Cultures.Tr));
    }

    [Fact]
    public void When_and_relative_texts()
    {
        Assert.Equal("14.41", ReminderText.When(At(26, 14, 41), Now));
        Assert.Equal("yarın 09.00", ReminderText.When(At(27, 9, 0), Now));
        Assert.Equal("28 Kasım Cumartesi 09.00", ReminderText.When(At(28, 9, 0), Now));
        Assert.Equal("9 dakika sonra (14.41)", ReminderText.Relative(At(26, 14, 41), Now));
        Assert.Equal("yarın 09.00", ReminderText.Relative(At(27, 9, 0), Now));
    }

    [Fact]
    public void Announcement_joins_names()
    {
        Reminder R(string text, bool missed = false, int hour = 14, int minute = 32) => new(Guid.NewGuid(), text, At(26, hour, minute), missed);

        Assert.Equal("Süre doldu: makarna.", ReminderText.Announcement([R("makarna")], Now, Cultures.Tr));
        Assert.Equal("Süre doldu: makarna ve çay.", ReminderText.Announcement([R("makarna"), R("çay")], Now, Cultures.Tr));
        Assert.Equal("Süre doldu: a, b ve c.", ReminderText.Announcement([R("a"), R("b"), R("c")], Now, Cultures.Tr));
        Assert.Equal("Kaçırılan hatırlatma: toplantı, 09.00.", ReminderText.Announcement([R("toplantı", true, 9, 0)], Now, Cultures.Tr));
        Assert.Equal("Süre doldu: makarna. Kaçırılan hatırlatmalar: toplantı (09.00) ve ilaç (10.00).",
            ReminderText.Announcement([R("makarna"), R("toplantı", true, 9, 0), R("ilaç", true, 10, 0)], Now, Cultures.Tr));
    }

    [Fact]
    public void English_announcement()
    {
        Reminder R(string text, bool missed = false, int day = 26, int hour = 14, int minute = 32) => new(Guid.NewGuid(), text, At(day, hour, minute), missed);

        Assert.Equal("Time's up: pasta.", ReminderText.Announcement([R("pasta")], Now, Cultures.En));
        Assert.Equal("Time's up: a, b and c.", ReminderText.Announcement([R("a"), R("b"), R("c")], Now, Cultures.En));
        Assert.Equal("Missed reminder: meeting, 9:00 AM.", ReminderText.Announcement([R("meeting", true, 26, 9, 0)], Now, Cultures.En));
        Assert.Equal("Time's up: pasta. Missed reminders: meeting (9:00 AM) and pills (10:00 AM).",
            ReminderText.Announcement([R("pasta"), R("meeting", true, 26, 9, 0), R("pills", true, 26, 10, 0)], Now, Cultures.En));
    }

    [Fact]
    public void English_when_texts()
    {
        Assert.Equal("2:41 PM", ReminderText.When(At(26, 14, 41), Now, Cultures.En));
        Assert.Equal("tomorrow 9:00 AM", ReminderText.When(At(27, 9, 0), Now, Cultures.En));
        Assert.Equal("Saturday, November 28, 9:00 AM", ReminderText.When(At(28, 9, 0), Now, Cultures.En));
        Assert.Equal("2:41 pm", ReminderText.When(At(26, 14, 41), Now, Cultures.EnGb));
        Assert.Equal("28 Kasım Cumartesi 09.00", ReminderText.When(At(28, 9, 0), Now, Cultures.Tr));
    }

    [Fact]
    public void Announcement_keeps_braces_in_reminder_text()
    {
        Reminder R(string text) => new(Guid.NewGuid(), text, At(26, 14, 32), false);
        Assert.Equal("Time's up: a {0} b.", ReminderText.Announcement([R("a {0} b")], Now, Cultures.En));
        Assert.Equal("Süre doldu: {x}.", ReminderText.Announcement([R("{x}")], Now, Cultures.Tr));
    }
}
