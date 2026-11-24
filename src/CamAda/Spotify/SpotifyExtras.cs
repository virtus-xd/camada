using System.Windows.Threading;
using CamAda.Core.Spotify;
using CamAda.Media;

namespace CamAda.Spotify;

/// <summary>
/// Kart ve panele beğen ve sıradaki şarkıları sağlar. Medya oturumu Spotify kimliği vermediği için parça değişince 1 sn
/// beklenip çalan parça Web API'den sorulur; dönen ad medya oturumundaki başlıkla eşleşmezse (Web API henüz eski parçayı
/// gösteriyorsa) 2 sn sonra bir kez daha denenir. Beğen düğmesi iyimser güncellenir, istek başarısızsa geri alınır.
/// </summary>
public sealed class SpotifyExtras : ISpotifyExtras
{
    private readonly SpotifyClient _client;
    private readonly NowPlayingService _media;
    private readonly DispatcherTimer _resolveTimer;
    private string? _trackKey;
    private string? _uri;
    private bool _retried;
    private bool _lastRequestOk = true;

    public SpotifyExtras(SpotifyClient client, NowPlayingService media, Dispatcher dispatcher)
    {
        _client = client;
        _media = media;
        _resolveTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher);
        _resolveTimer.Tick += async (_, _) =>
        {
            _resolveTimer.Stop();
            await ResolveAsync();
        };
        _media.Changed += (_, _) => OnMediaChanged();
        _client.ConnectionChanged += (_, _) =>
        {
            _trackKey = null;
            OnMediaChanged();
        };
        OnMediaChanged();
    }

    public bool IsAvailable => _client.IsConnected && _lastRequestOk;

    public bool? IsCurrentSaved { get; private set; }

    public IReadOnlyList<QueueItem> UpNext { get; private set; } = [];

    public event EventHandler? Changed;

    public async Task ToggleSavedAsync()
    {
        if (_uri is null || IsCurrentSaved is not { } saved)
        {
            return;
        }

        var uri = _uri;
        IsCurrentSaved = !saved;
        Changed?.Invoke(this, EventArgs.Empty);
        if (!await _client.SetSavedAsync(uri, !saved) && uri == _uri)
        {
            IsCurrentSaved = saved;
            Changed?.Invoke(this, EventArgs.Empty);
        }
    }

    public async Task RefreshAsync()
    {
        if (!_client.IsConnected)
        {
            return;
        }

        if (_uri is null)
        {
            await ResolveAsync();
            return;
        }

        var uri = _uri;
        var saved = await _client.IsSavedAsync(uri);
        var queue = await _client.QueueAsync(3);
        if (uri != _uri)
        {
            return; // bu sırada parça değişti: eski parçanın bilgisi yazılmaz
        }

        _lastRequestOk = saved is not null;
        IsCurrentSaved = saved;
        UpNext = queue;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Parça değişince eski bilgiler silinir ve 1 sn sonra yenisi sorulur.</summary>
    private void OnMediaChanged()
    {
        var track = _media.Current;
        var key = track is null ? null : $"{track.Title}\n{track.Artist}";
        if (key == _trackKey)
        {
            return;
        }

        _trackKey = key;
        _uri = null;
        _retried = false;
        IsCurrentSaved = null;
        UpNext = [];
        Changed?.Invoke(this, EventArgs.Empty);

        _resolveTimer.Stop();
        if (key is not null && _client.IsConnected)
        {
            _resolveTimer.Interval = TimeSpan.FromSeconds(1);
            _resolveTimer.Start();
        }
    }

    private async Task ResolveAsync()
    {
        if (_media.Current is not { } track || !_client.IsConnected)
        {
            return;
        }

        var key = _trackKey;
        var current = await _client.CurrentTrackAsync();
        if (key != _trackKey)
        {
            return; // bu sırada parça değişti; yenisi için zamanlayıcı zaten kuruldu
        }

        if (current is not { } found || !string.Equals(found.Title, track.Title, StringComparison.CurrentCultureIgnoreCase))
        {
            if (!_retried)
            {
                _retried = true;
                _resolveTimer.Interval = TimeSpan.FromSeconds(2);
                _resolveTimer.Start();
            }

            Changed?.Invoke(this, EventArgs.Empty);
            return;
        }

        _uri = found.Uri;
        await RefreshAsync();
    }
}
