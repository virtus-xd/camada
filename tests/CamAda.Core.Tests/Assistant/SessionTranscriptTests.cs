using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class SessionTranscriptTests
{
    [Fact]
    public void New_answer_audio_clears_previous_answer()
    {
        var t = new SessionTranscript();
        t.AdaAudio();
        t.AdaText("Saat on.");
        t.TurnComplete();

        t.AdaAudio(); // yeni yanıtın ilk ses parçası, yazısı henüz gelmedi

        Assert.Equal("", t.Ada); // satır bir önceki cümleyi göstermez
        t.AdaText("Hava güneşli.");
        Assert.Equal("Hava güneşli.", t.Ada);
    }

    [Fact]
    public void User_text_accumulates_and_restarts_after_turn()
    {
        var t = new SessionTranscript();
        t.UserText("sesi ");
        t.UserText("kıs");
        Assert.Equal("sesi kıs", t.User);

        t.TurnComplete();
        t.UserText("teşekkürler");
        Assert.Equal("teşekkürler", t.User);
    }

    [Fact]
    public void Interrupted_answer_is_not_continued()
    {
        var t = new SessionTranscript();
        t.AdaText("Napolyon 1769'da");
        t.Interrupted();
        t.AdaText("Tabii, kısaltıyorum.");

        Assert.Equal("Tabii, kısaltıyorum.", t.Ada);
    }
}
