using CamAda.Core.Clipboard;

namespace CamAda.Core.Tests.Clipboard;

public class ClipboardPolicyTests
{
    // Son inceleme, önemli 3: Office HTML/RTF'yi istek anında üretir; büyük kopyada bu, kabuğu ve panoyu kilitler.
    [Theory]
    [InlineData(0, true)]
    [InlineData(ClipboardPolicy.MaxTextBytesForRichFormats, true)]
    [InlineData(ClipboardPolicy.MaxTextBytesForRichFormats + 1, false)]
    [InlineData(16L * 1024 * 1024, false)]
    public void Rich_formats_only_for_small_text(long textBytes, bool expected) =>
        Assert.Equal(expected, ClipboardPolicy.ShouldReadRichFormats(textBytes));

    // Son inceleme, küçük 4 (yükseltildi): şifre işareti okunamıyorsa kopya saklanmaz (güvenli taraf).
    [Theory]
    [InlineData(false, false, false, null, false)]
    [InlineData(true, false, false, null, true)]
    [InlineData(false, true, false, null, true)]
    [InlineData(false, false, true, new byte[] { 0, 0, 0, 0 }, true)]
    [InlineData(false, false, true, new byte[] { 1, 0, 0, 0 }, false)]
    [InlineData(false, false, true, null, true)]
    [InlineData(false, false, true, new byte[] { 1, 0 }, true)]
    public void Flagged_or_unreadable_flag_is_excluded(bool exclude, bool viewerIgnore, bool canIncludePresent, byte[]? canInclude, bool expected) =>
        Assert.Equal(expected, ClipboardPolicy.IsExcluded(exclude, viewerIgnore, canIncludePresent, canInclude));
}
