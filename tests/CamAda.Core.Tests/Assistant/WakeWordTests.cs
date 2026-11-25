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
}
