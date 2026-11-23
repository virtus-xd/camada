using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CamAda.Core.Apps;
using ManagedShell;
using ManagedShell.Common.Enums;
using ManagedShell.Common.Helpers;
using ManagedShell.WindowsTasks;

namespace CamAda.Island;

/// <summary>
/// ManagedShell'in pencere listesini ve sabitlenmiş uygulamaları izleyip adadaki düğmeleri kurar.
/// Değişiklikler 150 ms'lik yığınlamayla tek bir yeniden kuruluşa indirgenir.
/// </summary>
public sealed class DockViewModel : IDisposable
{
    private readonly ShellManager _shell;
    private readonly Func<IReadOnlyList<PinnedApp>> _pinnedApps;
    private readonly DispatcherTimer _rebuild;
    private readonly Dictionary<string, ImageSource?> _iconCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ApplicationWindow> _watched = new();
    private Dictionary<nint, ApplicationWindow> _byHandle = new();

    public DockViewModel(ShellManager shell, Func<IReadOnlyList<PinnedApp>> pinnedApps, Dispatcher dispatcher)
    {
        _shell = shell;
        _pinnedApps = pinnedApps;
        _rebuild = new DispatcherTimer(TimeSpan.FromMilliseconds(150), DispatcherPriority.Background, (_, _) => Rebuild(), dispatcher);
        _rebuild.Stop();

        if (_shell.Tasks?.GroupedWindows is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged += OnWindowsChanged;
        }

        Rebuild();
    }

    public ObservableCollection<DockItemViewModel> Items { get; } = new();

    public IReadOnlyList<DockEntry> Overflow { get; private set; } = [];

    public event EventHandler? Changed;

    /// <summary>RunningWindow.Handle → ManagedShell penceresi (öne getirme, küçültme, kapatma ve simge için).</summary>
    public ApplicationWindow? Find(nint handle) => _byHandle.GetValueOrDefault(handle);

    /// <summary>Ayarlar değişince (ör. sabitlenmişler) dışarıdan yeniden kurulum ister.</summary>
    public void Refresh() => ScheduleRebuild();

    public void Dispose()
    {
        _rebuild.Stop();
        if (_shell.Tasks?.GroupedWindows is INotifyCollectionChanged collection)
        {
            collection.CollectionChanged -= OnWindowsChanged;
        }

        foreach (var window in _watched)
        {
            window.PropertyChanged -= OnWindowPropertyChanged;
        }

        _watched.Clear();
    }

    private void OnWindowsChanged(object? sender, NotifyCollectionChangedEventArgs e) => ScheduleRebuild();

    private void OnWindowPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ApplicationWindow.State) or nameof(ApplicationWindow.Icon)
            or nameof(ApplicationWindow.OverlayIcon) or nameof(ApplicationWindow.Title))
        {
            ScheduleRebuild();
        }
    }

    private void ScheduleRebuild()
    {
        _rebuild.Stop();
        _rebuild.Start();
    }

    private void Rebuild()
    {
        _rebuild.Stop();

        var windows = _shell.Tasks?.GroupedWindows?.Cast<ApplicationWindow>().ToList() ?? [];
        WatchWindows(windows);
        _byHandle = windows.ToDictionary(w => w.Handle);

        var running = windows.Select(w => new RunningWindow(
            w.Handle,
            Path.GetFileName(w.WinFileName ?? "") ?? "",
            w.Title ?? "",
            w.State == ApplicationWindow.WindowState.Active,
            w.State == ApplicationWindow.WindowState.Flashing,
            w.IsMinimized,
            string.IsNullOrEmpty(w.AppUserModelID) ? null : w.AppUserModelID,
            w.OverlayIcon is not null)).ToList();

        var split = DockOverflow.Split(DockGrouping.Group(_pinnedApps(), running));
        Overflow = split.Overflow;

        Items.Clear();
        var firstUnpinnedSeen = false;
        foreach (var entry in split.Visible)
        {
            var firstUnpinned = entry.Pinned is null && !firstUnpinnedSeen && split.Visible.Any(e => e.Pinned is not null);
            firstUnpinnedSeen |= entry.Pinned is null;
            Items.Add(DockItemViewModel.ForEntry(entry, IconFor(entry), BadgeFor(entry), IsMissing(entry), firstUnpinned));
        }

        if (split.Overflow.Count > 0)
        {
            Items.Add(DockItemViewModel.ForOverflow(split.Overflow));
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void WatchWindows(IReadOnlyList<ApplicationWindow> windows)
    {
        foreach (var gone in _watched.Where(w => !windows.Contains(w)).ToList())
        {
            gone.PropertyChanged -= OnWindowPropertyChanged;
            _watched.Remove(gone);
        }

        foreach (var window in windows.Where(w => _watched.Add(w)))
        {
            window.PropertyChanged += OnWindowPropertyChanged;
        }
    }

    private ImageSource? IconFor(DockEntry entry)
    {
        if (entry.Pinned is { } pinned)
        {
            if (!_iconCache.TryGetValue(pinned.LaunchPath, out var icon))
            {
                icon = LoadPinnedIcon(pinned.LaunchPath);
                _iconCache[pinned.LaunchPath] = icon;
            }

            return icon;
        }

        if (entry.Windows.Count == 0)
        {
            return null;
        }

        // Bazı iletişim kutusu pencereleri (winver, colorcpl…) hiç simge vermez ve ManagedShell boş bırakır;
        // görev çubuğunun yaptığı gibi exe'nin simgesine düş. Pencere simgesi sonradan gelirse yeniden kurulumda o seçilir.
        var window = Find(entry.Windows[0].Handle);
        return window?.Icon ?? ExecutableIcon(window?.WinFileName);
    }

    private ImageSource? ExecutableIcon(string? path)
    {
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        var key = "exe:" + path; // sabitlenmişlerin (app.ico olabilir) önbellek anahtarlarıyla karışmasın
        if (!_iconCache.TryGetValue(key, out var icon))
        {
            icon = IconImageConverter.GetImageFromAssociatedIcon(path, IconSize.Large);
            _iconCache[key] = icon;
        }

        return icon;
    }

    /// <summary>
    /// Sabitlenmiş uygulamanın simgesi. Squirrel ile kurulan uygulamalar (Discord, Slack…) Update.exe üzerinden başlar;
    /// onların asıl simgesi yanındaki app.ico dosyasındadır.
    /// </summary>
    private static ImageSource? LoadPinnedIcon(string launchPath)
    {
        if (!File.Exists(launchPath))
        {
            return IconImageConverter.GetDefaultIcon();
        }

        var appIco = Path.Combine(Path.GetDirectoryName(launchPath) ?? "", "app.ico");
        if (File.Exists(appIco))
        {
            try
            {
                var decoder = new IconBitmapDecoder(new Uri(appIco), BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
                var largest = decoder.Frames.OrderByDescending(f => f.PixelWidth).First();
                largest.Freeze();
                return largest;
            }
            catch (Exception ex) when (ex is IOException or FileFormatException or NotSupportedException)
            {
                // Bozuk .ico: exe'nin kendi simgesine düş.
            }
        }

        return IconImageConverter.GetImageFromAssociatedIcon(launchPath, IconSize.Large);
    }

    private ImageSource? BadgeFor(DockEntry entry) =>
        entry.Windows.Select(w => Find(w.Handle)?.OverlayIcon).FirstOrDefault(i => i is not null);

    private static bool IsMissing(DockEntry entry) =>
        entry.Pinned is { } pinned && !File.Exists(pinned.LaunchPath);
}
