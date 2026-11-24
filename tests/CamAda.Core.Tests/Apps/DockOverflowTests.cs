using CamAda.Core.Apps;
using CamAda.Core.Island;

namespace CamAda.Core.Tests.Apps;

public class DockOverflowTests
{
    private static DockEntry Pinned(int i) => new($"pin:{i}", new PinnedApp($"p{i}", $"P{i}", $@"C:\p{i}.exe", "", $"p{i}.exe"), []);
    private static DockEntry Running(int i) => new($"run:{i}", null, [new RunningWindow(i, $"r{i}.exe", "t", false, false, false, null, false)]);

    [Fact]
    public void Eight_entries_all_visible()
    {
        var split = DockOverflow.Split(Enumerable.Range(0, 8).Select(Running).ToList());
        Assert.Equal(8, split.Visible.Count);
        Assert.Empty(split.Overflow);
    }

    [Fact]
    public void Overflow_takes_the_last_unpinned_entries()
    {
        var entries = Enumerable.Range(0, 4).Select(Pinned).Concat(Enumerable.Range(0, 9).Select(Running)).ToList();
        var split = DockOverflow.Split(entries);
        Assert.Equal(10, split.Visible.Count);
        Assert.Equal(entries.Take(10), split.Visible);
        Assert.Equal(entries.Skip(10), split.Overflow);
    }

    [Fact]
    public void Pinned_entries_are_never_cut()
    {
        var entries = Enumerable.Range(0, 12).Select(Pinned).ToList();
        var split = DockOverflow.Split(entries);
        Assert.Equal(12, split.Visible.Count);
        Assert.Empty(split.Overflow);
    }
}

public class IslandLayoutTests
{
    // Plan 3: genişlemiş satıra 150 DIP müzik bölümü eklendi (770 + 150 = 920); Plan 4.1: 36 DIP ses düğmesi (956).
    // Üst sınır 1250.
    [Theory]
    [InlineData(0, 956)]
    [InlineData(5, 956)]
    [InlineData(6, 994)]
    [InlineData(11, 1184)]
    [InlineData(30, 1250)]
    public void Expanded_width_grows_with_app_buttons(int buttons, double expected) =>
        Assert.Equal(expected, IslandLayout.ExpandedWidthFor(buttons));
}
