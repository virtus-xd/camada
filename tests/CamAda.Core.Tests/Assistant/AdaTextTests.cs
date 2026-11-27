using CamAda.Core.Assistant;
using static CamAda.Core.Tests.Text.Cultures;

namespace CamAda.Core.Tests.Assistant;

public class AdaTextTests
{
    [Fact]
    public void Last_finished_sentence_is_shown() =>
        Assert.Equal("Ses düzeyini yüzde otuza getirdim.", AdaText.LastSentence("Tamam. Ses düzeyini yüzde otuza getirdim.", 80));

    [Fact]
    public void Sentence_in_progress_is_shown() =>
        Assert.Equal("Şimdi bakıyorum", AdaText.LastSentence("Tamam! Şimdi bakıyorum", 80));

    [Fact]
    public void Long_sentence_keeps_its_end() =>
        Assert.Equal("…dört beş", AdaText.LastSentence("bir iki üç dört beş", 9));

    [Fact]
    public void Blank_is_empty() => Assert.Equal("", AdaText.LastSentence("  ", 80));

    [Theory]
    [InlineData("Emin misin?", true)]
    [InlineData("Emin misin? ", true)]
    [InlineData("“Kapatayım mı?”", true)]
    [InlineData("Sesi kıstım.", false)]
    [InlineData("", false)]
    [InlineData("Kıstım. Başka bir şey?", true)]
    [InlineData("Ne dersin? Kıstım.", false)]
    public void Ends_with_question(string text, bool expected) => Assert.Equal(expected, AdaText.EndsWithQuestion(text));

    [Fact]
    public void Listening_line_mentions_name_in_guest_mode()
    {
        Assert.Equal("“Ada” deyin, dinliyorum", AdaText.ListeningLine(guest: true, Tr));
        Assert.Equal("Dinliyorum…", AdaText.ListeningLine(guest: false, Tr));
    }
}
