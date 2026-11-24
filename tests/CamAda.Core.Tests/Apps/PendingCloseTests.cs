using CamAda.Core.Apps;

namespace CamAda.Core.Tests.Apps;

public class PendingCloseTests
{
    [Fact]
    public void Closed_window_removes_card() =>
        Assert.Equal(PendingCloseOutcome.Remove, PendingCloseRule.Decide(windowExists: false, TimeSpan.FromMilliseconds(150)));

    [Fact]
    public void Window_still_closing_keeps_waiting() =>
        Assert.Equal(PendingCloseOutcome.Wait, PendingCloseRule.Decide(windowExists: true, TimeSpan.FromSeconds(1)));

    // Uygulama kapatmayı reddetti (ör. yönetici penceresi ya da "Kaydedilsin mi?"de İptal): kart geri gelir.
    [Fact]
    public void Window_that_refused_to_close_restores_card() =>
        Assert.Equal(PendingCloseOutcome.Restore, PendingCloseRule.Decide(windowExists: true, PendingCloseRule.GiveUpAfter));
}
