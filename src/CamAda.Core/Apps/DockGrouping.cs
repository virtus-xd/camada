namespace CamAda.Core.Apps;

public static class DockGrouping
{
    /// <summary>
    /// Penceresini ana exe yerine yardımcı bir süreçte açan uygulamalar: yardımcının pencereleri ana uygulamanın
    /// sabitlenmiş düğmesine bağlanır (yeni Steam istemcisinin penceresi steamwebhelper.exe'dedir).
    /// </summary>
    private static readonly Dictionary<string, string> HelperProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        ["steamwebhelper.exe"] = "steam.exe",
    };

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
            var pinIndex = IsSeparateApp(window) ? -1 : IndexOfPinned(pinned, window.ProcessName);
            if (pinIndex < 0 && HelperProcesses.TryGetValue(window.ProcessName, out var mainProcess))
            {
                pinIndex = IndexOfPinned(pinned, mainProcess);
            }

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

    /// <summary>
    /// Sabitlenmiş uygulamanın exe'sinde çalışan ama Windows'un ayrı uygulama saydığı pencereler (kendi
    /// AppUserModelId'leriyle): Chromium web uygulamaları ("Chrome._crx_…") ve explorer.exe'deki Denetim Masası gibi
    /// pencereler (normal Gezgin penceresinde kimlik boştur). Bunlar sabitlenmemiş gibi kendi kimlikleriyle gruplanır.
    /// </summary>
    private static bool IsSeparateApp(RunningWindow window) =>
        window.AppUserModelId is { } id
        && (id.Contains("_crx_", StringComparison.OrdinalIgnoreCase)
            || (string.Equals(window.ProcessName, "explorer.exe", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(id, "Microsoft.Windows.Explorer", StringComparison.OrdinalIgnoreCase)));

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
