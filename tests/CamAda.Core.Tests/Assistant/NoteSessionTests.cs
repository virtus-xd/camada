using CamAda.Core.Assistant;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Assistant;

public class NoteSessionTests
{
    private readonly FakeTimeProvider _time = new();

    private NoteSession Make() => new(_time, NoteSession.Quiet);

    private void Wait(double seconds) => _time.Advance(TimeSpan.FromSeconds(seconds));

    [Fact]
    public void Parts_join_on_new_lines_and_text_accumulates()
    {
        var n = Make();
        n.Start();

        Assert.Equal("Süt al.", n.Append("  Süt al. "));
        Assert.Equal("\nAnneni ara.", n.Append("Anneni ara."));
        Assert.Equal("", n.Append("   "));
        Assert.Equal("Süt al.\nAnneni ara.", n.Text);
    }

    [Fact]
    public void Finishes_after_4s_without_user_speech_or_writes()
    {
        var n = Make();
        n.Start();
        n.UserSpoke();
        Wait(3);
        n.Append("Süt al.");
        Wait(3.9);
        Assert.False(n.ShouldFinish());

        Wait(0.2);
        Assert.True(n.ShouldFinish());
        Assert.True(n.Finish());
        Assert.False(n.Active);
        Assert.False(n.ShouldFinish());
        Assert.False(n.Finish());
    }

    [Fact]
    public void Only_note_tools_allowed()
    {
        Assert.True(NoteSession.Allows(AdaTools.NotYaz));
        Assert.True(NoteSession.Allows(AdaTools.NotBitir));
        Assert.False(NoteSession.Allows(AdaTools.SesDegistir)); // notta geçen "sesi kıs" yürütülmez
        Assert.False(NoteSession.Allows(AdaTools.BilgisayariKapat));
        Assert.False(NoteSession.Allows(AdaTools.NotAl));
    }

    [Fact]
    public void Mutes_from_first_user_speech_after_1s_until_finish()
    {
        var n = Make();
        n.Start();
        Wait(0.5);
        n.UserSpoke(); // komutun geç gelen yazısı: "Dinliyorum" kısılmasın
        Assert.False(n.Muted);

        Wait(1);
        n.UserSpoke();
        Assert.True(n.Muted);
        n.Finish();
        Assert.False(n.Muted);
    }

    [Fact]
    public void Late_part_after_finish_appends_to_same_note()
    {
        var n = Make();
        n.Start();
        n.Append("Süt al.");
        n.Finish();

        Assert.Equal("\nEkmek de.", n.Append("Ekmek de."));
        Assert.False(n.Active);
    }

    [Fact]
    public void Reset_forgets_note_and_write_without_start_is_refused()
    {
        var n = Make();
        n.Start();
        n.UserSpoke();
        Wait(2);
        n.UserSpoke();
        n.Reset();

        Assert.False(n.Active);
        Assert.False(n.Muted);
        Assert.Null(n.Append("x"));
    }

    [Fact]
    public void File_name_is_date_and_minute() =>
        Assert.Equal("Not 26.11.2026 01.07.txt", NoteSession.FileNameFor(new DateTime(2026, 11, 26, 1, 7, 42)));

    [Fact]
    public void Waits_8s_for_first_part_then_4s_between_parts()
    {
        var n = Make();
        n.Start();
        Wait(7.9); // "Dinliyorum"dan sonra kullanıcının başlaması zaman alır; canlı yazı da söz bitince gelir
        Assert.False(n.ShouldFinish());

        n.UserSpoke();
        Wait(7.9);
        Assert.False(n.ShouldFinish());

        n.Append("Süt al.");
        Wait(3.9);
        Assert.False(n.ShouldFinish());
        Wait(0.2);
        Assert.True(n.ShouldFinish());
    }

    [Fact]
    public void Nothing_said_after_start_finishes_at_8s()
    {
        var n = Make();
        n.Start();
        Wait(8.1);
        Assert.True(n.ShouldFinish());
    }

    [Fact]
    public void Same_part_twice_in_a_row_is_written_once()
    {
        var n = Make();
        n.Start();
        n.Append("Süt al.");
        Assert.Equal("\nAnneni ara.", n.Append("Anneni ara."));

        Assert.Equal("", n.Append(" Anneni ara. ")); // model aynı parçayı yeniden gönderdi (26.11 sahte oda)
        Assert.Equal("Süt al.\nAnneni ara.", n.Text);
        Assert.Equal("\nSüt al.", n.Append("Süt al.")); // art arda değilse yazılır
    }
}
