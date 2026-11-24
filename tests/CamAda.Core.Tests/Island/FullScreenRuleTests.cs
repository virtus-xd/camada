using CamAda.Core.Glass;
using CamAda.Core.Island;

namespace CamAda.Core.Tests.Island;

public class FullScreenRuleTests
{
    private static readonly RectD Screen = new(0, 0, 1920, 1080);

    private static FullScreenCandidate Candidate(RectD? bounds = null, bool visible = true, bool minimized = false,
        bool maximized = false, bool hasCaption = false, bool reportedByShell = false) =>
        new(bounds ?? Screen, Screen, visible, minimized, maximized, hasCaption, reportedByShell);

    [Fact]
    public void Borderless_window_covering_screen_is_full_screen() =>
        Assert.True(FullScreenRule.IsFullScreen(Candidate()));

    // Görev çubuğu gizliyken büyütülmüş VS Code / Chrome: istemci alanı tam monitöre eşit ama tam ekran değil.
    [Fact]
    public void Maximized_window_with_caption_is_not_full_screen() =>
        Assert.False(FullScreenRule.IsFullScreen(Candidate(maximized: true, hasCaption: true)));

    [Fact]
    public void Maximized_window_without_caption_is_full_screen() =>
        Assert.True(FullScreenRule.IsFullScreen(Candidate(maximized: true)));

    [Fact]
    public void Shell_reported_maximized_window_with_caption_is_not_full_screen() =>
        Assert.False(FullScreenRule.IsFullScreen(Candidate(maximized: true, hasCaption: true, reportedByShell: true)));

    // ManagedShell listesi yalnız etkinleşmede güncellenir: küçültülmüş pencere bir süre listede kalabilir.
    [Fact]
    public void Window_no_longer_covering_screen_is_not_full_screen() =>
        Assert.False(FullScreenRule.IsFullScreen(Candidate(new RectD(100, 100, 800, 600), hasCaption: true)));

    [Fact]
    public void Shell_reported_window_is_trusted_without_bounds_match() =>
        Assert.True(FullScreenRule.IsFullScreen(Candidate(new RectD(0, 0, 1920, 1040), reportedByShell: true)));

    [Fact]
    public void Minimized_window_is_not_full_screen() =>
        Assert.False(FullScreenRule.IsFullScreen(Candidate(minimized: true)));

    [Fact]
    public void Hidden_window_is_not_full_screen() =>
        Assert.False(FullScreenRule.IsFullScreen(Candidate(visible: false)));
}
