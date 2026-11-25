using CamAda.Core.Clipboard;

namespace CamAda.Core.Tests.Clipboard;

public class ScreenshotNameTests
{
    [Fact]
    public void Name_has_date_and_seconds() =>
        Assert.Equal("Ekran görüntüsü 2026-11-26 021530.png", ScreenshotName.For(new DateTime(2026, 11, 26, 2, 15, 30)));
}
