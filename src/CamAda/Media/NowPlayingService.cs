using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using CamAda.Core.Media;
using Windows.Media.Control;

namespace CamAda.Media;

/// <summary>
/// Spotify'ın Windows medya oturumunu izler (spec §8). Ada, Müzik paneli, plak ve kart aynı örneği dinler; birinde duraklatınca
/// hepsi aynı durumu görür (spec §7.8). WinRT olayları arka plan iş parçacığından gelir: okumalar tek kapıdan sıralanır,
/// sonuç <see cref="Changed"/> ile arayüz iş parçacığında yayınlanır. Kapak yalnız parça değişince yeniden çözülür.
/// </summary>
public sealed class NowPlayingService : IDisposable
{
    private const int CoverPixels = 300;

    private readonly Dispatcher _dispatcher;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private GlobalSystemMediaTransportControlsSessionManager? _manager;
    private GlobalSystemMediaTransportControlsSession? _session;
    private string? _coverKey;
    private bool _coverFound;
    private bool _disposed;

    private NowPlayingService(Dispatcher dispatcher)
    {
        _dispatcher = dispatcher;
    }

    /// <summary>Spotify oturumu yoksa (ya da parça bilgisi boşsa) null.</summary>
    public NowPlaying? Current { get; private set; }

    /// <summary>Son çalma anı: çalarken her güncellemede, çalma durunca durduğu an.</summary>
    public DateTimeOffset? LastPlayingAt { get; private set; }

    /// <summary>Donmuş, en fazla 300 px kapak; kapak yoksa null.</summary>
    public ImageSource? Cover { get; private set; }

    public Rgb CoverColor { get; private set; } = DominantColor.Fallback;

    /// <summary>Parça, durum, zaman çizelgesi ya da kapak değişince (arayüz iş parçacığında).</summary>
    public event EventHandler? Changed;

    /// <summary>Medya oturumu yöneticisine bağlanır; Windows izin vermezse servis boş kalır (Current hep null), çökmez.</summary>
    public static async Task<NowPlayingService> CreateAsync(Dispatcher dispatcher)
    {
        var service = new NowPlayingService(dispatcher);
        try
        {
            service._manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
            service._manager.SessionsChanged += service.OnSessionsChanged;
            await service.SelectSessionAsync();
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            Debug.WriteLine($"Medya oturumlarına bağlanılamadı: {ex.Message}");
        }

        return service;
    }

    public Task TogglePlayPauseAsync() => Control(s => s.TryTogglePlayPauseAsync().AsTask());

    public Task NextAsync() => Control(s => s.TrySkipNextAsync().AsTask());

    public Task PreviousAsync() => Control(s => s.TrySkipPreviousAsync().AsTask());

    /// <summary>Spotify'ı açar ("spotify:" adresi); yüklü değilse sessiz kalır.</summary>
    public void OpenSpotify()
    {
        try
        {
            Process.Start(new ProcessStartInfo("spotify:") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Spotify açılamadı: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _disposed = true;
        if (_manager is not null)
        {
            _manager.SessionsChanged -= OnSessionsChanged;
        }

        Unsubscribe(_session);
        _session = null;
    }

    private static bool IsMediaFailure(Exception ex) =>
        ex is COMException or UnauthorizedAccessException or InvalidOperationException or ObjectDisposedException;

    private async Task Control(Func<GlobalSystemMediaTransportControlsSession, Task<bool>> action)
    {
        if (_session is not { } session)
        {
            return;
        }

        try
        {
            await action(session);
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            Debug.WriteLine($"Medya denetimi reddedildi: {ex.Message}");
        }
    }

    private async void OnSessionsChanged(GlobalSystemMediaTransportControlsSessionManager sender, SessionsChangedEventArgs args) =>
        await SelectSessionAsync();

    private async void OnMediaPropertiesChanged(GlobalSystemMediaTransportControlsSession sender, MediaPropertiesChangedEventArgs args) =>
        await RefreshAsync();

    private async void OnPlaybackInfoChanged(GlobalSystemMediaTransportControlsSession sender, PlaybackInfoChangedEventArgs args) =>
        await RefreshAsync();

    private async void OnTimelinePropertiesChanged(GlobalSystemMediaTransportControlsSession sender, TimelinePropertiesChangedEventArgs args) =>
        await RefreshAsync();

    /// <summary>Oturumlar değişince Spotify oturumunu yeniden seçer (Spotify kapanıp açılınca yeniden başlatma gerekmez).</summary>
    private async Task SelectSessionAsync()
    {
        if (_manager is null || _disposed)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            var sessions = _manager.GetSessions();
            var index = MediaSourcePicker.PickSpotify(sessions.Select(s => s.SourceAppUserModelId ?? "").ToList());
            var picked = index >= 0 ? sessions[index] : null;

            if (picked?.SourceAppUserModelId != _session?.SourceAppUserModelId || picked is null != _session is null)
            {
                Unsubscribe(_session);
                _session = picked;
                _coverKey = null;
                if (picked is not null)
                {
                    picked.MediaPropertiesChanged += OnMediaPropertiesChanged;
                    picked.PlaybackInfoChanged += OnPlaybackInfoChanged;
                    picked.TimelinePropertiesChanged += OnTimelinePropertiesChanged;
                }
            }
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            Debug.WriteLine($"Medya oturumları okunamadı: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }

        await RefreshAsync();
    }

    private void Unsubscribe(GlobalSystemMediaTransportControlsSession? session)
    {
        if (session is null)
        {
            return;
        }

        session.MediaPropertiesChanged -= OnMediaPropertiesChanged;
        session.PlaybackInfoChanged -= OnPlaybackInfoChanged;
        session.TimelinePropertiesChanged -= OnTimelinePropertiesChanged;
    }

    /// <summary>Oturumun parça, durum ve zaman bilgisini okur; parça değiştiyse kapağı çözer ve sonucu yayınlar.</summary>
    private async Task RefreshAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _gate.WaitAsync();
        try
        {
            NowPlaying? track = null;
            var coverChanged = false;
            BitmapSource? cover = null;

            if (_session is { } session)
            {
                var properties = await session.TryGetMediaPropertiesAsync();
                var playback = session.GetPlaybackInfo();
                var timeline = session.GetTimelineProperties();

                if (!string.IsNullOrWhiteSpace(properties?.Title))
                {
                    track = new NowPlaying(
                        session.SourceAppUserModelId ?? "",
                        properties.Title,
                        properties.Artist ?? "",
                        properties.AlbumTitle ?? "",
                        playback?.PlaybackStatus switch
                        {
                            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Playing => PlaybackState.Playing,
                            GlobalSystemMediaTransportControlsSessionPlaybackStatus.Paused => PlaybackState.Paused,
                            _ => PlaybackState.Stopped,
                        },
                        timeline?.Position ?? TimeSpan.Zero,
                        timeline is null ? TimeSpan.Zero : timeline.EndTime - timeline.StartTime,
                        timeline?.LastUpdatedTime ?? DateTimeOffset.Now);

                    var key = $"{track.Title}\n{track.Artist}\n{track.Album}";
                    if (CoverRule.ShouldLoad(key, _coverKey, _coverFound, properties.Thumbnail is not null))
                    {
                        _coverKey = key;
                        coverChanged = true;
                        cover = await LoadCoverAsync(properties.Thumbnail);
                        _coverFound = cover is not null;
                    }
                }
                else
                {
                    coverChanged = _coverKey is not null;
                    _coverKey = null;
                }
            }
            else
            {
                coverChanged = _coverKey is not null || Cover is not null;
                _coverKey = null;
            }

            var color = cover is null ? DominantColor.Fallback : DominantColor.From(Pixels(cover));
            _ = _dispatcher.BeginInvoke(() => Publish(track, coverChanged, cover, color));
        }
        catch (Exception ex) when (IsMediaFailure(ex))
        {
            Debug.WriteLine($"Medya bilgisi okunamadı: {ex.Message}");
        }
        finally
        {
            _gate.Release();
        }
    }

    private void Publish(NowPlaying? track, bool coverChanged, BitmapSource? cover, Rgb color)
    {
        if (_disposed)
        {
            return;
        }

        var now = DateTimeOffset.Now;
        if (track?.State == PlaybackState.Playing || Current?.State == PlaybackState.Playing)
        {
            LastPlayingAt = now; // çalarken her güncellemede, çalma durunca durduğu an (geçiş payı buradan sayılır)
        }

        Current = track;
        if (coverChanged)
        {
            Cover = cover;
            CoverColor = color;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Küçük resmi okuyup en fazla 300 px'lik donmuş bir bitmap'e çözer; okunamazsa null (kapaksız etiket).</summary>
    private static async Task<BitmapSource?> LoadCoverAsync(Windows.Storage.Streams.IRandomAccessStreamReference? thumbnail)
    {
        if (thumbnail is null)
        {
            return null;
        }

        try
        {
            using var stream = await thumbnail.OpenReadAsync();
            using var source = stream.AsStream();
            var buffer = new MemoryStream();
            await source.CopyToAsync(buffer);
            buffer.Position = 0;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = CoverPixels;
            image.StreamSource = buffer;
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch (Exception ex) when (IsMediaFailure(ex) || ex is IOException or NotSupportedException or FileFormatException
                                       or ArgumentException)
        {
            Debug.WriteLine($"Kapak okunamadı: {ex.Message}");
            return null;
        }
    }

    private static byte[] Pixels(BitmapSource image)
    {
        var converted = new FormatConvertedBitmap(image, PixelFormats.Bgra32, null, 0);
        var stride = converted.PixelWidth * 4;
        var pixels = new byte[stride * converted.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        return pixels;
    }
}
