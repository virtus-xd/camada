using CamAda.Core.Assistant;

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
}
