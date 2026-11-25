using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CamAda.Core.Clipboard;
using CamAda.Core.Collections;

namespace CamAda.Clips;

/// <summary>Ekran görüntüsü klasöründeki bir dosya.</summary>
public sealed record ScreenshotFile(string Path, DateTime Created, int Width, int Height);

/// <summary>
/// Ekran görüntüleri (tasarım "Ekran görüntüleri"): Windows'un ekran görüntüsü klasörünü izler, son görüntüleri ve
/// küçük resimlerini verir, panoya gelen görselleri <see cref="ScreenshotTracker"/> ile dosyalarla eşleştirir (aynı ekran
/// görüntüsü tek kayıt). Klasör yoksa üst klasörü izler, klasör oluşunca ona geçer.
/// </summary>
public sealed class ScreenshotLibrary : IDisposable
{
    private static readonly string[] Extensions = [".png", ".jpg", ".jpeg"];
    private static readonly Guid ScreenshotsFolderId = new("B7BEDE81-DF94-4682-A7D8-57A52620B86F");
    private const uint KF_FLAG_DONT_VERIFY = 0x4000; // klasör henüz yoksa da yolunu ver (yoksa Resimler\Screenshots'a düşerdik)

    private readonly Dispatcher _dispatcher;
    private readonly ClipboardService _clips;
    private readonly Action<Exception> _log;
    private readonly ScreenshotTracker _tracker = new(TimeProvider.System);
    private readonly Dictionary<string, ClipPayload> _pending = new();
    // Şerit en çok 10 görüntü gösterir; sınır, yıllarca alınan ekran görüntülerinin küçük resimlerinin birikmesini önler.
    private readonly LruCache<string, ImageSource> _thumbnails = new(capacity: 32, StringComparer.OrdinalIgnoreCase);
    private readonly DispatcherTimer _expiry;
    private FileSystemWatcher? _watcher;
    private bool _disposed;

    public ScreenshotLibrary(Dispatcher dispatcher, ClipboardService clips, string? folder, Action<Exception> log)
    {
        _dispatcher = dispatcher;
        _clips = clips;
        _log = log;
        Folder = folder;
        _expiry = new DispatcherTimer(TimeSpan.FromMilliseconds(500), DispatcherPriority.Background, (_, _) => TakeExpired(), dispatcher);
        _expiry.Stop();
        _clips.ImageArrived += OnClipImage;
        Watch();
    }

    public string? Folder { get; }

    /// <summary>Yeni ekran görüntüsü dosyası (UI iş parçacığında).</summary>
    public event EventHandler<ScreenshotFile>? Arrived;

    /// <summary>Dosyası olmayan, yalnız panoya giden ekran görüntüsü; geçmişe eklenmiş öğeyle (UI iş parçacığında).</summary>
    public event EventHandler<ClipItem>? ClipboardOnlyShot;

    /// <summary>Windows'un ekran görüntüsü klasörü (kullanıcı taşıdıysa yeni yeri); bulunamazsa Resimler\Screenshots.</summary>
    public static string? KnownFolder()
    {
        if (SHGetKnownFolderPath(ScreenshotsFolderId, KF_FLAG_DONT_VERIFY, 0, out var pointer) == 0)
        {
            try
            {
                return Marshal.PtrToStringUni(pointer);
            }
            finally
            {
                Marshal.FreeCoTaskMem(pointer);
            }
        }

        var pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
        return string.IsNullOrEmpty(pictures) ? null : Path.Combine(pictures, "Screenshots");
    }

    /// <summary>En yeni <paramref name="count"/> ekran görüntüsü, en yenisi önce.</summary>
    public IReadOnlyList<ScreenshotFile> Recent(int count = 10)
    {
        if (Folder is null || !Directory.Exists(Folder))
        {
            return [];
        }

        try
        {
            return new DirectoryInfo(Folder).EnumerateFiles()
                .Where(f => IsImage(f.Name))
                .OrderByDescending(f => f.CreationTimeUtc)
                .Take(count)
                .Select(f => new ScreenshotFile(f.FullName, f.CreationTime, 0, 0))
                .ToList();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log(ex);
            return [];
        }
    }

    /// <summary>Şerit için küçük resim (192 piksel genişlik, önbellekli); açılamazsa null.</summary>
    public ImageSource? Thumbnail(ScreenshotFile file)
    {
        if (_thumbnails.TryGet(file.Path, out var cached))
        {
            return cached;
        }

        try
        {
            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.CreateOptions = BitmapCreateOptions.IgnoreColorProfile;
            image.UriSource = new Uri(file.Path);
            image.DecodePixelWidth = 192;
            image.EndInit();
            image.Freeze();
            _thumbnails.Set(file.Path, image);
            return image;
        }
        catch (Exception ex) when (ex is IOException or NotSupportedException or UnauthorizedAccessException or FileFormatException)
        {
            return null;
        }
    }

    /// <summary>Görüntüyü Ekran Alıntısı Aracı'nda düzenlemeye açar; açılamazsa Paint'te.</summary>
    public static void OpenForEdit(string path)
    {
        try
        {
            Process.Start(new ProcessStartInfo(
                $"ms-screensketch:edit?source=CamAda&isTemporary=false&filePath={Uri.EscapeDataString(path)}") { UseShellExecute = true });
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            try
            {
                Process.Start(new ProcessStartInfo("mspaint.exe", $"\"{path}\"") { UseShellExecute = true });
            }
            catch (Exception fallback) when (fallback is System.ComponentModel.Win32Exception or InvalidOperationException)
            {
                Debug.WriteLine($"Ekran görüntüsü düzenlemeye açılamadı: {ex.Message} / {fallback.Message}");
            }
        }
    }

    public static void ShowInFolder(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true });

    public static void OpenFolder(string folder) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"\"{folder}\"") { UseShellExecute = true });

    public void Dispose()
    {
        _disposed = true;
        _expiry.Stop();
        _clips.ImageArrived -= OnClipImage;
        _watcher?.Dispose();
    }

    private void Watch()
    {
        if (Folder is null || _disposed)
        {
            return;
        }

        try
        {
            if (Directory.Exists(Folder))
            {
                _watcher = new FileSystemWatcher(Folder) { NotifyFilter = NotifyFilters.FileName };
                _watcher.Created += (_, e) => _ = OnFileAsync(e.FullPath);
                _watcher.Renamed += (_, e) => _ = OnFileAsync(e.FullPath);
                _watcher.Error += OnWatcherError;
                _watcher.EnableRaisingEvents = true;
                return;
            }

            // Klasör henüz yok (hiç ekran görüntüsü alınmamış): üst klasörü izle, oluşunca ona geç. Gezgin'de "Yeni
            // klasör" sonra yeniden adlandırma Created değil Renamed verir.
            var parent = Path.GetDirectoryName(Folder);
            if (parent is null || !Directory.Exists(parent))
            {
                return;
            }

            var name = Path.GetFileName(Folder);
            _watcher = new FileSystemWatcher(parent) { NotifyFilter = NotifyFilters.DirectoryName };
            FileSystemEventHandler onAppeared = (_, e) =>
            {
                if (string.Equals(e.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    _dispatcher.BeginInvoke(Rewatch);
                }
            };
            _watcher.Created += onAppeared;
            _watcher.Renamed += (sender, e) => onAppeared(sender, e);
            _watcher.Error += OnWatcherError;
            _watcher.EnableRaisingEvents = true;
        }
        catch (Exception ex) when (ex is IOException or ArgumentException or UnauthorizedAccessException)
        {
            _log(ex);
        }
    }

    /// <summary>İzleyici durdu (arabellek taştı, klasör silindi ya da ağ sürücüsü koptu): bir süre sonra yeniden kur.</summary>
    private void OnWatcherError(object sender, ErrorEventArgs e)
    {
        _log(e.GetException());
        _dispatcher.BeginInvoke(async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
            Rewatch();
        });
    }

    private void Rewatch()
    {
        _watcher?.Dispose();
        _watcher = null;
        Watch();
    }

    /// <summary>İzleyici iş parçacığında: dosya yazımı bitince boyutunu okuyup UI'a geçer.</summary>
    private async Task OnFileAsync(string path)
    {
        if (!IsImage(path) || !ScreenshotTracker.IsNewFile(File.GetCreationTimeUtc(path), DateTimeOffset.UtcNow))
        {
            return; // taşınan ya da yeniden adlandırılan eski dosya yeni ekran görüntüsü değildir
        }

        (int Width, int Height)? size = null;
        for (var attempt = 0; attempt < 20 && size is null; attempt++)
        {
            try
            {
                using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
                var frame = BitmapDecoder.Create(stream, BitmapCreateOptions.DelayCreation, BitmapCacheOption.None).Frames[0];
                size = (frame.PixelWidth, frame.PixelHeight);
            }
            catch (IOException)
            {
                await Task.Delay(100); // ekran alıntısı aracı hâlâ yazıyor
            }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or UnauthorizedAccessException)
            {
                return;
            }
        }

        if (size is not { } s)
        {
            return;
        }

        _ = _dispatcher.BeginInvoke(() =>
        {
            if (_disposed)
            {
                return;
            }

            if (_tracker.OnFile(s.Width, s.Height) is { } claim)
            {
                if (claim.InHistory)
                {
                    _clips.Remove(claim.Id);
                }
                else
                {
                    _pending.Remove(claim.Id);
                    UpdateExpiryTimer();
                }
            }

            Arrived?.Invoke(this, new ScreenshotFile(path, File.GetCreationTime(path), s.Width, s.Height));
        });
    }

    private void OnClipImage(object? sender, ClipImageArrival arrival)
    {
        var payload = arrival.Payload;
        var pendingId = Guid.NewGuid().ToString("N");
        switch (_tracker.OnClipImage(pendingId, payload.Width, payload.Height, arrival.FromScreenshotTool))
        {
            case ClipImageDecision.AddToHistory:
                // Yalnız yeni ve sabitlenmemiş öğe izlenir: aynı boyutta dosya gelince silinecek olan odur. Geçmişte zaten
                // olan (ör. sabitli) görselin kopyası yeniden gelince silinmemeli.
                var existed = _clips.History.Contains(payload);
                if (_clips.AddImage(payload) is { Pinned: false } item && !existed)
                {
                    _tracker.AddedToHistory(item.Id, payload.Width, payload.Height);
                }

                break;
            case ClipImageDecision.Hold:
                _pending[pendingId] = payload;
                UpdateExpiryTimer();
                break;
        }
    }

    private void TakeExpired()
    {
        foreach (var id in _tracker.TakeExpired())
        {
            if (_pending.Remove(id, out var payload) && _clips.AddImage(payload) is { } item)
            {
                ClipboardOnlyShot?.Invoke(this, item);
            }
        }

        UpdateExpiryTimer();
    }

    private void UpdateExpiryTimer()
    {
        if (_tracker.HasPending)
        {
            _expiry.Start();
        }
        else
        {
            _expiry.Stop();
        }
    }

    private static bool IsImage(string path) =>
        Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SHGetKnownFolderPath([MarshalAs(UnmanagedType.LPStruct)] Guid id, uint flags, nint token, out nint path);
}
