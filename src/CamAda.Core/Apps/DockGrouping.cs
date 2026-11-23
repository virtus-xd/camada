namespace CamAda.Core.Apps;

public static class DockGrouping
{
    /// <summary>
    /// Önce sabitlenmişler (ayar sırasıyla, açık olmasalar da), sonra sabitlenmemiş açık uygulamalar ilk görülme sırasıyla.
    /// Pencereler sabitlenmiş uygulamaya exe adıyla (büyük/küçük harf duyarsız) bağlanır; sabitlenmemişler
    /// AppUserModelId varsa ona, yoksa exe adına göre gruplanır.
    /// </summary>
    public static IReadOnlyList<DockEntry> Group(IReadOnlyList<PinnedApp> pinned, IReadOnlyList<RunningWindow> windowsInOrder)
    {
        var pinnedWindows = pinned.Select(_ => new List<RunningWindow>()).ToList();
        var unpinned = new List<(string Key, List<RunningWindow> Windows)>();

        foreach (var window in windowsInOrder)
        {
            var pinIndex = IndexOfPinned(pinned, window.ProcessName);
            if (pinIndex >= 0)
            {
                pinnedWindows[pinIndex].Add(window);
                continue;
            }

            var key = window.AppUserModelId ?? window.ProcessName.ToLowerInvariant();
            var group = unpinned.FindIndex(g => g.Key == key);
            if (group >= 0)
            {
                unpinned[group].Windows.Add(window);
            }
            else
            {
                unpinned.Add((key, [window]));
            }
        }

        var entries = new List<DockEntry>(pinned.Count + unpinned.Count);
        for (var i = 0; i < pinned.Count; i++)
        {
            entries.Add(new DockEntry("pin:" + pinned[i].Id, pinned[i], pinnedWindows[i]));
        }

        entries.AddRange(unpinned.Select(g => new DockEntry("run:" + g.Key, null, g.Windows)));
        return entries;
    }

    private static int IndexOfPinned(IReadOnlyList<PinnedApp> pinned, string processName)
    {
        for (var i = 0; i < pinned.Count; i++)
        {
            if (string.Equals(pinned[i].ProcessName, processName, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
