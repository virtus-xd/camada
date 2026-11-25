using CamAda.Core.Assistant;

namespace CamAda.Core.Tests.Assistant;

public class UsageLedgerTests
{
    [Fact]
    public void Same_month_accumulates()
    {
        var first = UsageLedger.Add(null, new DateOnly(2026, 11, 25), 0.5m, sessions: 1, searches: 2);
        Assert.Equal(new UsageMonth("2026-11", 0.5m, 1, 2), first);
        Assert.Equal(new UsageMonth("2026-11", 0.75m, 2, 2), UsageLedger.Add(first, new DateOnly(2026, 11, 30), 0.25m, 1, 0));
    }

    [Fact]
    public void New_month_restarts() =>
        Assert.Equal(new UsageMonth("2026-12", 0.1m, 1, 0),
            UsageLedger.Add(new UsageMonth("2026-11", 3m, 40, 12), new DateOnly(2026, 12, 1), 0.1m, 1, 0));
}
