using CamAda.Core.Clipboard;

namespace CamAda.Core.Tests.Clipboard;

public class PasteRuleTests
{
    private static readonly PasteTarget Normal = new(Exists: true, IsShellOrSelf: false, Elevated: false);

    [Fact]
    public void Opened_by_button_only_copies()
    {
        Assert.Equal(PasteOutcome.CopyOnly, PasteRule.Decide(openedByHotkey: false, Normal));
        Assert.Equal(PasteOutcome.CopyOnly, PasteRule.Decide(openedByHotkey: false, null));
    }

    [Fact]
    public void No_target_only_copies() => Assert.Equal(PasteOutcome.CopyOnly, PasteRule.Decide(true, null));

    [Fact]
    public void Desktop_or_camada_only_copies() =>
        Assert.Equal(PasteOutcome.CopyOnly, PasteRule.Decide(true, Normal with { IsShellOrSelf = true }));

    [Fact]
    public void Closed_target_copies_with_hint() =>
        Assert.Equal(PasteOutcome.CopyWithHint, PasteRule.Decide(true, Normal with { Exists = false }));

    [Fact]
    public void Elevated_target_copies_with_hint() =>
        Assert.Equal(PasteOutcome.CopyWithHint, PasteRule.Decide(true, Normal with { Elevated = true }));

    [Fact]
    public void Normal_target_pastes() => Assert.Equal(PasteOutcome.Paste, PasteRule.Decide(true, Normal));
}
