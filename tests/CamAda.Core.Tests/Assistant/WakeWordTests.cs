using System.Globalization;
using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class WakeWordTests
{
    [Fact]
    public void Keyword_line_uses_invariant_numbers()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("▁HE Y ▁A D A :1.2 #0.2 @HEY_ADA", WakeWord.KeywordLine(0.2));
            Assert.Equal("▁HE Y ▁A D A :1.2 #0.35 @HEY_ADA", WakeWord.KeywordLine(0.35));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void Name_line_has_tokens_boost_threshold_label()
    {
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("tr-TR");
            Assert.Equal("▁A D A :1.2 #0.25 @ADA", WakeWord.NameKeywordLine(0.25, 1.2));
            Assert.Equal("▁A D A :1.5 #0.07 @ADA", WakeWord.NameKeywordLine(WakeWord.NameThreshold, WakeWord.NameBoost));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }
}
