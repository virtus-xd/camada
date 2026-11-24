using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CamAda.Core.Clipboard;
using CamAda.Interop;

namespace CamAda.Clips;

/// <summary>Panelde seçilen şey: geçmişteki bir öğe ya da şeritteki bir ekran görüntüsü.</summary>
public abstract record ClipChoice;

public sealed record HistoryChoice(string ItemId) : ClipChoice;

public sealed record ScreenshotChoice(string Path) : ClipChoice;

/// <summary>Panoya gelen görsel; ekran alıntısı aracından mı geldiği pano sahibinin süreç adından anlaşılır.</summary>
public sealed record ClipImageArrival(ClipPayload Payload, bool FromScreenshotTool);

/// <summary>
/// Pano geçmişinin canlı kaynağı (tasarım "Geçmiş"): Windows pano dinleyicisiyle her kopyayı okur, işaretli (şifre
/// yöneticisi) kopyaları içeriğe bakmadan eler, geri kalanı <see cref="ClipboardHistory"/>'ye ekler. Pano yalnız baytları
/// almak için kısa süre açılır; görsel çözme ve özetleme arka planda yapılır. CamAda'nın kendi yazımları yeni kopya
/// sayılmaz. Sabitlenenler diske yazılır, gerisi yalnız bellekte durur.
/// </summary>
public sealed class ClipboardService : IDisposable
{
    private const int MaxOpenAttempts = 5;
    private const int MaxBusyAttempts = 20; // pano sahibi en çok ~3 sn meşgul kalabilir
    private const long MaxDibBytes = 160L * 1024 * 1024; // 8K ekranın 32 bit DIB'i sığsın; saklanan PNG yine 16 MB'la sınırlı
    private static readonly HwndSourceParameters WindowParameters = new("CamAda pano") { ParentWindow = new IntPtr(-3), WindowStyle = 0 };

    private static readonly uint ExcludeFormat = Native.RegisterClipboardFormat("ExcludeClipboardContentFromMonitorProcessing");
    private static readonly uint ViewerIgnoreFormat = Native.RegisterClipboardFormat("Clipboard Viewer Ignore");
    private static readonly uint CanIncludeFormat = Native.RegisterClipboardFormat("CanIncludeInClipboardHistory");
    private static readonly uint HtmlFormat = Native.RegisterClipboardFormat("HTML Format");
    private static readonly uint RtfFormat = Native.RegisterClipboardFormat("Rich Text Format");
    private static readonly uint PngFormat = Native.RegisterClipboardFormat("PNG");

    private readonly PinnedClipStore _store;
    private readonly Action<Exception> _log;
    private readonly HwndSource _source;
    private readonly DispatcherTimer _settle;
    private readonly Dictionary<string, ImageSource> _thumbnails = new();
    private int _attempts;
    private int _busyAttempts;
    private string? _ownerAtUpdate;
    private uint _ownSequence;
    private int _savedVersion;
    private bool _disposed;

    public ClipboardService(Dispatcher dispatcher, string pinnedDirectory, Action<Exception> log)
    {
        _store = new PinnedClipStore(pinnedDirectory);
        _log = log;
        IReadOnlyList<ClipItem> pinned = [];
        try
        {
            pinned = _store.Load();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            log(ex);
        }

        History = new ClipboardHistory(pinned);
        _savedVersion = History.PinnedVersion;

        // Uygulamalar panoyu birkaç adımda doldurabilir: son bildirimden kısa süre sonra bir kez okunur.
        _settle = new DispatcherTimer(DispatcherPriority.Background, dispatcher) { Interval = TimeSpan.FromMilliseconds(60) };
        _settle.Tick += (_, _) =>
        {
            _settle.Stop();
            ReadClipboard();
        };

        _source = new HwndSource(WindowParameters);
        _source.AddHook(Hook);
        if (!Native.AddClipboardFormatListener(_source.Handle))
        {
            log(new InvalidOperationException($"Pano dinleyicisi kurulamadı ({Marshal.GetLastWin32Error()})."));
        }
    }

    public ClipboardHistory History { get; }

    /// <summary>Geçmiş değişti (yeni kopya, sabitleme, silme). UI iş parçacığında.</summary>
    public event EventHandler? Changed;

    public void Pin(string id) => Mutate(() => History.Pin(id));

    public void Unpin(string id) => Mutate(() => History.Unpin(id));

    public void Remove(string id) => Mutate(() => History.Remove(id));

    public void ClearUnpinned() => Mutate(History.ClearUnpinned);

    /// <summary>Görsel öğenin küçük resmi (80 piksel genişlik, önbellekli).</summary>
    public ImageSource? ThumbnailFor(ClipItem item)
    {
        if (item.Payload.Png is not { } png || item.Payload.PixelHash is not { } hash)
        {
            return null;
        }

        if (!_thumbnails.TryGetValue(hash, out var thumbnail))
        {
            try
            {
                thumbnail = Decode(png, decodeWidth: 80);
            }
            catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or IOException
                                           or COMException)
            {
                return null;
            }

            _thumbnails[hash] = thumbnail;
        }

        return thumbnail;
    }

    /// <summary>Seçileni panoya yazar (kendi yazımımız geçmişe yeni kopya olarak girmez). Başarısızsa false.</summary>
    public async Task<bool> PutAsync(ClipChoice choice)
    {
        DataObject? data;
        try
        {
            data = choice switch
            {
                HistoryChoice h => History.Find(h.ItemId) is { } item ? ToDataObject(item.Payload) : null,
                ScreenshotChoice s => FromFile(s.Path),
                _ => null,
            };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException
                                       or ArgumentException or COMException)
        {
            _log(ex);
            return false;
        }

        if (data is null)
        {
            return false;
        }

        for (var attempt = 0; attempt < MaxOpenAttempts; attempt++)
        {
            try
            {
                System.Windows.Clipboard.SetDataObject(data, copy: true);
                _ownSequence = Native.GetClipboardSequenceNumber();
                if (choice is HistoryChoice history)
                {
                    Mutate(() => History.Touch(history.ItemId, DateTimeOffset.Now));
                }

                return true;
            }
            catch (Exception ex) when (ex is COMException or ExternalException)
            {
                await Task.Delay(50);
            }
        }

        return false;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _settle.Stop();
        Native.RemoveClipboardFormatListener(_source.Handle);
        _source.RemoveHook(Hook);
        _source.Dispose();
    }

    private IntPtr Hook(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == Native.WM_CLIPBOARDUPDATE)
        {
            _attempts = 0;
            _ownerAtUpdate = Native.ClipboardOwnerProcessName(); // .NET/OLE yazanlarda okuma anında sahip çoktan boşalmış olabilir
            _busyAttempts = 0;
            _settle.Interval = TimeSpan.FromMilliseconds(60);
            _settle.Stop();
            _settle.Start();
        }

        return IntPtr.Zero;
    }

    private void ReadClipboard()
    {
        if (_disposed || Native.GetClipboardSequenceNumber() == _ownSequence)
        {
            return;
        }

        // Sahip hâlâ panoya yazıyorsa (ör. .NET uygulamalarının "OleFlushClipboard" adımı) onu bekletmeden geri çekil.
        var owner = Native.GetClipboardOwner();
        if (owner != 0 && !Native.IsResponsive(owner, 100))
        {
            if (++_busyAttempts < MaxBusyAttempts)
            {
                _settle.Interval = TimeSpan.FromMilliseconds(150);
                _settle.Start();
            }

            return;
        }

        if (!Native.OpenClipboard(_source.Handle))
        {
            // Başka bir uygulama panoyu tutuyor: UI'ı uyutmadan kısa aralıklarla yeniden dene.
            if (++_attempts < MaxOpenAttempts)
            {
                _settle.Interval = TimeSpan.FromMilliseconds(50);
                _settle.Start();
            }

            return;
        }

        Snapshot? snapshot;
        try
        {
            snapshot = TakeSnapshot();
        }
        finally
        {
            Native.CloseClipboard();
        }

        if (snapshot is { Text: { } text })
        {
            Mutate(() => History.Add(ClipPayload.FromText(text, snapshot.Html, snapshot.Rtf), DateTimeOffset.Now));
        }
        else if (snapshot is { Png: not null } or { Dib: not null })
        {
            _ = AddImageAsync(snapshot);
        }
    }

    /// <summary>Açık panonun içeriği; işaretli, dosya listesi, boş ya da çok büyükse null.</summary>
    private Snapshot? TakeSnapshot()
    {
        var canIncludePresent = Native.IsClipboardFormatAvailable(CanIncludeFormat);
        if (ClipboardPolicy.IsExcluded(
                Native.IsClipboardFormatAvailable(ExcludeFormat),
                Native.IsClipboardFormatAvailable(ViewerIgnoreFormat),
                canIncludePresent,
                canIncludePresent ? Native.ReadClipboardBytes(CanIncludeFormat, 64, out _) : null))
        {
            return null;
        }

        if (Native.IsClipboardFormatAvailable(Native.CF_HDROP))
        {
            return null;
        }

        var owner = Native.ClipboardOwnerProcessName() ?? _ownerAtUpdate;
        var max = ClipboardHistory.MaxItemBytes;
        if (Native.IsClipboardFormatAvailable(Native.CF_UNICODETEXT))
        {
            var bytes = Native.ReadClipboardBytes(Native.CF_UNICODETEXT, max, out var tooBig);
            if (tooBig)
            {
                return null;
            }

            var text = bytes is null ? null : TrimAtNul(Encoding.Unicode.GetString(bytes));
            if (!string.IsNullOrWhiteSpace(text))
            {
                if (!ClipboardPolicy.ShouldReadRichFormats(bytes!.LongLength))
                {
                    return new Snapshot(text, null, null, null, null, owner);
                }

                var html = Native.ReadClipboardBytes(HtmlFormat, max, out _) is { } h ? TrimAtNul(Encoding.UTF8.GetString(h)) : null;
                var rtf = Native.ReadClipboardBytes(RtfFormat, max, out _) is { } r ? TrimAtNul(Encoding.Latin1.GetString(r)) : null;
                return new Snapshot(text, html, rtf, null, null, owner);
            }
        }

        if (Native.IsClipboardFormatAvailable(PngFormat))
        {
            var png = Native.ReadClipboardBytes(PngFormat, max, out var tooBig);
            return tooBig || png is null ? null : new Snapshot(null, null, null, png, null, owner);
        }

        if (Native.IsClipboardFormatAvailable(Native.CF_DIB))
        {
            var dib = Native.ReadClipboardBytes(Native.CF_DIB, MaxDibBytes, out _);
            return dib is null ? null : new Snapshot(null, null, null, null, dib, owner);
        }

        return null;
    }

    private async Task AddImageAsync(Snapshot snapshot)
    {
        ClipPayload? payload;
        try
        {
            payload = await Task.Run(() => ClipImages.Prepare(snapshot.Png, snapshot.Dib));
        }
        catch (Exception ex) when (ex is NotSupportedException or FileFormatException or ArgumentException or OverflowException
                                       or IOException or COMException or OutOfMemoryException)
        {
            _log(ex);
            return;
        }

        if (payload is not null && !_disposed)
        {
            OnImage(payload, snapshot.Owner);
        }
    }

    /// <summary>
    /// Görsel geldi: dinleyici varsa (ekran görüntüsü eşleştirmesi) karar onundur, yoksa doğrudan geçmişe eklenir.
    /// </summary>
    public event EventHandler<ClipImageArrival>? ImageArrived;

    /// <summary>Görseli geçmişe ekler (eşleştirmenin "geçmişe ekle" kararı için).</summary>
    public ClipItem? AddImage(ClipPayload payload)
    {
        ClipItem? item = null;
        Mutate(() => item = History.Add(payload, DateTimeOffset.Now));
        return item;
    }

    private void OnImage(ClipPayload payload, string? owner)
    {
        if (ImageArrived is { } handler)
        {
            handler(this, new ClipImageArrival(payload, owner is "SnippingTool" or "ScreenClippingHost"));
        }
        else
        {
            AddImage(payload);
        }
    }


    private void Mutate(Action change)
    {
        change();
        SavePinnedIfChanged();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void SavePinnedIfChanged()
    {
        if (History.PinnedVersion == _savedVersion)
        {
            return;
        }

        _savedVersion = History.PinnedVersion;
        try
        {
            _store.Save(History.Pinned);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log(ex);
        }
    }

    private static DataObject ToDataObject(ClipPayload payload)
    {
        var data = new DataObject();
        if (payload.Png is { } png)
        {
            data.SetImage(Decode(png, decodeWidth: 0));
            data.SetData("PNG", new MemoryStream(png));
            return data;
        }

        data.SetData(DataFormats.UnicodeText, payload.Text ?? "");
        if (payload.Html is { } html)
        {
            data.SetData(DataFormats.Html, html);
        }

        if (payload.Rtf is { } rtf)
        {
            data.SetData(DataFormats.Rtf, rtf);
        }

        return data;
    }

    private static DataObject FromFile(string path)
    {
        var bytes = File.ReadAllBytes(path);
        var data = new DataObject();
        data.SetImage(Decode(bytes, decodeWidth: 0));
        if (path.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
        {
            data.SetData("PNG", new MemoryStream(bytes));
        }

        return data;
    }

    /// <summary>Görsel baytlarını dondurulmuş bir görüntüye çözer; <paramref name="decodeWidth"/> 0 ise tam boyut.</summary>
    internal static BitmapSource Decode(byte[] bytes, int decodeWidth)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.StreamSource = new MemoryStream(bytes);
        if (decodeWidth > 0)
        {
            image.DecodePixelWidth = decodeWidth;
        }

        image.EndInit();
        image.Freeze();
        return image;
    }

    private static string TrimAtNul(string text)
    {
        var nul = text.IndexOf('\0');
        return nul >= 0 ? text[..nul] : text;
    }

    private sealed record Snapshot(string? Text, string? Html, string? Rtf, byte[]? Png, byte[]? Dib, string? Owner);
}
