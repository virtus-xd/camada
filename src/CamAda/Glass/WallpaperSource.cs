using System.IO;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CamAda.Core.Glass;
using CamAda.Interop;
using Microsoft.Win32;

namespace CamAda.Glass;

/// <summary>Geçerli duvar kağıdını ve yerleşim stilini okur; değişince <see cref="Changed"/> tetikler.</summary>
public sealed class WallpaperSource : IDisposable
{
    private readonly DispatcherTimer _debounce;

    public WallpaperSource(Dispatcher dispatcher)
    {
        // Duvar kağıdı değişirken birkaç olay arka arkaya gelir; tek bir yenilemeye indir.
        _debounce = new DispatcherTimer(TimeSpan.FromMilliseconds(400), DispatcherPriority.Background, (_, _) =>
        {
            _debounce!.Stop();
            Changed?.Invoke(this, EventArgs.Empty);
        }, dispatcher);
        _debounce.Stop();

        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged += OnDisplaySettingsChanged;
    }

    public event EventHandler? Changed;

    /// <summary>Duvar kağıdı görüntüsü; düz renk arka planda ya da okunamazsa null.</summary>
    public BitmapSource? LoadImage()
    {
        var path = Native.GetWallpaperPath();
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            var frame = BitmapFrame.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            frame.Freeze();
            return frame;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    public WallpaperStyle ReadStyle()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop");
        return WallpaperStyleParser.Parse(key?.GetValue("WallpaperStyle") as string, key?.GetValue("TileWallpaper") as string);
    }

    public void Dispose()
    {
        _debounce.Stop();
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        SystemEvents.DisplaySettingsChanged -= OnDisplaySettingsChanged;
    }

    private void OnUserPreferenceChanged(object? sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.Desktop)
        {
            Restart();
        }
    }

    private void OnDisplaySettingsChanged(object? sender, EventArgs e) => Restart();

    private void Restart() => _debounce.Dispatcher.BeginInvoke(() =>
    {
        _debounce.Stop();
        _debounce.Start();
    });
}
