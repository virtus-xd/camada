namespace CamAda.Core.Apps;

public sealed record DockSplit(IReadOnlyList<DockEntry> Visible, IReadOnlyList<DockEntry> Overflow);

/// <summary>Adada en fazla 10 uygulama düğmesi görünür; fazlası "+N" düğmesinde toplanır.</summary>
public static class DockOverflow
{
    public const int MaxVisible = 10;

    /// <summary>Sabitlenmişler her zaman görünür kalır; taşma sabitlenmemişlerin sonundan alınır.</summary>
    public static DockSplit Split(IReadOnlyList<DockEntry> entries)
    {
        var pinnedCount = entries.Count(e => e.Pinned is not null);
        var visibleCount = Math.Max(MaxVisible, pinnedCount);
        if (entries.Count <= visibleCount)
        {
            return new DockSplit(entries, []);
        }

        return new DockSplit(entries.Take(visibleCount).ToList(), entries.Skip(visibleCount).ToList());
    }
}
