using System.Globalization;
using System.IO;
using System.Windows.Media;
using CamAda.Core.Search;
using ManagedShell.Common.Enums;
using ManagedShell.Common.Helpers;
using ManagedShell.UWPInterop;

namespace CamAda.Panels;

/// <summary>
/// Başlat panelinin aradığı öğeler: Başlat menüsü kısayolları, Store uygulamaları, Ayarlar sayfaları ve son dosyalar.
/// Arka planda bir kez kurulur; 10 dakikadan eskiyse istek anında eskisi dönerken arka planda yenilenir.
/// </summary>
public sealed class AppIndex
{
    private const string AppsFolderPrefix = @"shell:AppsFolder\";
    private const int RecentInSearch = 50;
    private static readonly TimeSpan RefreshAfter = TimeSpan.FromMinutes(10);

    /// <summary>Kaldırıcı, yardım ve belge kısayolları uygulama listesini kirletmesin.</summary>
    private static readonly string[] ExcludedWords = ["Uninstall", "Kaldır", "Help", "Yardım", "Readme"];

    private static readonly string RecentFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Recent");

    private readonly object _gate = new();
    private readonly Dictionary<string, ImageSource?> _icons = new(StringComparer.OrdinalIgnoreCase);
    private Task<IReadOnlyList<SearchItem>>? _building;
    private IReadOnlyList<SearchItem>? _items;
    private DateTime _builtAt;

    public Task<IReadOnlyList<SearchItem>> GetItemsAsync()
    {
        lock (_gate)
        {
            var stale = _items is not null && DateTime.UtcNow - _builtAt > RefreshAfter;
            if (_building is null || (stale && _building.IsCompleted))
            {
                _building = Task.Run(Build);
            }

            // Yenilenirken bekletme: elde bir önceki liste varsa onu ver.
            return _items is { } previous && !_building.IsCompleted ? Task.FromResult(previous) : _building;
        }
    }

    /// <summary>En son açılan dosyalar (Windows'un "Son kullanılanlar" klasöründeki kısayollar), en yenisi önce.</summary>
    public IReadOnlyList<SearchItem> RecentFiles(int count)
    {
        try
        {
            return new DirectoryInfo(RecentFolder).EnumerateFiles("*.lnk")
                .OrderByDescending(f => f.LastWriteTimeUtc)
                .Take(count)
                .Select(f => new SearchItem(Path.GetFileNameWithoutExtension(f.Name), SearchKind.File, f.FullName))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }

    /// <summary>Öğenin simgesi (UI iş parçacığında çağrılır, önbellekli). Ayar ve web öğelerinde null: panel kendi simgesini çizer.</summary>
    public ImageSource? IconFor(SearchItem item)
    {
        if (item.Kind is SearchKind.Setting or SearchKind.Web)
        {
            return null;
        }

        if (!_icons.TryGetValue(item.Target, out var icon))
        {
            icon = item.Target.StartsWith(AppsFolderPrefix, StringComparison.OrdinalIgnoreCase)
                ? StoreAppHelper.AppList.GetAppByAumid(item.Target[AppsFolderPrefix.Length..])?.GetIconImageSource(IconSize.Large)
                : IconImageConverter.GetImageFromAssociatedIcon(item.Target, IconSize.Large);
            _icons[item.Target] = icon;
        }

        return icon;
    }

    private IReadOnlyList<SearchItem> Build()
    {
        var items = new List<SearchItem>();
        var names = new HashSet<string>(StringComparer.Create(CultureInfo.GetCultureInfo("tr-TR"), ignoreCase: true));

        foreach (var folder in new[] { ShellHelper.UsersStartMenuPath, ShellHelper.AllUsersStartMenuPath })
        {
            foreach (var shortcut in SafeEnumerate(folder))
            {
                var name = Path.GetFileNameWithoutExtension(shortcut);
                if (!IsExcluded(name) && names.Add(name))
                {
                    items.Add(new SearchItem(name, SearchKind.App, shortcut));
                }
            }
        }

        try
        {
            StoreAppHelper.AppList.FetchApps();
            foreach (var app in StoreAppHelper.AppList)
            {
                if (!string.IsNullOrWhiteSpace(app.DisplayName) && !IsExcluded(app.DisplayName) && names.Add(app.DisplayName))
                {
                    items.Add(new SearchItem(app.DisplayName, SearchKind.App, AppsFolderPrefix + app.AppUserModelId));
                }
            }
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException or InvalidOperationException)
        {
            // Store listesi alınamadı: kısayollar ve ayarlarla devam.
        }

        items.AddRange(SettingsPages.All);
        items.AddRange(RecentFiles(RecentInSearch));

        lock (_gate)
        {
            _items = items;
            _builtAt = DateTime.UtcNow;
        }

        return items;
    }

    private static bool IsExcluded(string name) =>
        ExcludedWords.Any(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> SafeEnumerate(string folder)
    {
        if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
        {
            return [];
        }

        try
        {
            return Directory.EnumerateFiles(folder, "*.lnk", new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
            }).ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return [];
        }
    }
}
