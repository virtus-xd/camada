using System.ComponentModel;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json.Nodes;
using CamAda.Core.Spotify;

namespace CamAda.Spotify;

/// <summary>
/// Spotify Web API istemcisi (spec §5: beğen ve sıradaki şarkılar; Ada için arama ve çalma; Şubat 2026 uç noktaları). İstekten önce süresi
/// dolan belirteç yenilenir; 401'de bir kez yenileyip tekrar denenir; yenileme reddedilirse belirteç silinir (bağlantı kopar);
/// 429'da Retry-After süresince yeni istek atılmaz; ağ hatası sessizce null ya da boş döner. Bağlı değilken istek atılmaz.
/// </summary>
public sealed class SpotifyClient
{
    private const string Api = "https://api.spotify.com/v1";
    private static readonly HttpClient SharedHttp = CreateClient(new SocketsHttpHandler());

    private readonly HttpClient _http;
    private readonly Func<string?> _clientId;
    private readonly SpotifyTokenStore _store;
    private readonly TimeProvider _time;
    private SpotifyToken? _token;
    private Task<bool>? _refreshing;
    private Task<bool>? _connecting;
    private CancellationTokenSource? _connectCancel;
    private DateTimeOffset _blockedUntil;

    public SpotifyClient(Func<string?> clientId, SpotifyTokenStore store, TimeProvider time)
        : this(clientId, store, time, SharedHttp)
    {
    }

    /// <summary>Denemeler için: istekler verilen ağ katmanından geçer.</summary>
    public SpotifyClient(Func<string?> clientId, SpotifyTokenStore store, TimeProvider time, HttpMessageHandler handler)
        : this(clientId, store, time, CreateClient(handler))
    {
    }

    private SpotifyClient(Func<string?> clientId, SpotifyTokenStore store, TimeProvider time, HttpClient http)
    {
        _http = http;
        _clientId = clientId;
        _store = store;
        _time = time;
        _token = store.Load();
    }

    public string? ClientId => _clientId();

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_clientId());

    public bool IsConnected => IsConfigured && _token is not null;

    /// <summary>Bağlanınca ya da bağlantı kopunca.</summary>
    public event EventHandler? ConnectionChanged;

    /// <summary>
    /// Tarayıcıda Spotify izin sayfasını açar, yerel geri dönüşü (en fazla 2 dk) bekler, durumu doğrular ve kodu belirteçle
    /// takas eder. Kullanıcı reddederse, port meşgulse ya da süre dolarsa false. Önceki giriş hâlâ bekliyorsa (ör. panel
    /// kapanıp yeniden açıldı) o iptal edilir ve portu bırakması beklenir.
    /// </summary>
    public async Task<bool> ConnectAsync(CancellationToken cancellation)
    {
        _connectCancel?.Cancel();
        if (_connecting is { } previous)
        {
            await previous;
        }

        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        _connectCancel = cancel;
        var attempt = ConnectOnceAsync(cancel.Token);
        _connecting = attempt;
        try
        {
            return await attempt;
        }
        finally
        {
            if (_connecting == attempt)
            {
                _connecting = null;
                _connectCancel = null;
            }
        }
    }

    private async Task<bool> ConnectOnceAsync(CancellationToken cancellation)
    {
        if (_clientId() is not { Length: > 0 } clientId)
        {
            return false;
        }

        var verifier = SpotifyAuth.CreateVerifier();
        var state = SpotifyAuth.CreateState();
        // Takas geri dönüşün içinde yapılır (sayfa sonucu söylesin); belirteç bu bağlamda kaydedilir (olaylar UI'da).
        var callback = LoopbackCallback.WaitAsync(state,
            code => RequestTokenAsync(SpotifyAuth.CodeExchangeForm(clientId, code, verifier), previousRefreshToken: null),
            TimeSpan.FromMinutes(2), cancellation);
        try
        {
            Process.Start(new ProcessStartInfo(SpotifyAuth.AuthorizeUrl(clientId, SpotifyAuth.ChallengeFor(verifier), state))
            {
                UseShellExecute = true,
            })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            Debug.WriteLine($"Tarayıcı açılamadı: {ex.Message}");
            return false;
        }

        var token = await callback;
        if (token is null)
        {
            return false;
        }

        SetToken(token);
        return true;
    }

    public async Task<(string Uri, string Title)?> CurrentTrackAsync() =>
        await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"{Api}/me/player/currently-playing")) is { } body
            ? SpotifyJson.CurrentTrack(body)
            : null;

    public async Task<bool?> IsSavedAsync(string uri) =>
        await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"{Api}/me/library/contains?uris={Uri.EscapeDataString(uri)}"))
            is { } body
            ? SpotifyJson.Contains(body)
            : null;

    /// <summary>Beğenilenlere ekler (PUT) ya da çıkarır (DELETE); başarılıysa true.</summary>
    public async Task<bool> SetSavedAsync(string uri, bool saved) =>
        await SendAsync(() => new HttpRequestMessage(saved ? HttpMethod.Put : HttpMethod.Delete,
            $"{Api}/me/library?uris={Uri.EscapeDataString(uri)}")) is not null;

    public async Task<IReadOnlyList<QueueItem>> QueueAsync(int max) =>
        await SendAsync(() => new HttpRequestMessage(HttpMethod.Get, $"{Api}/me/player/queue")) is { } body
            ? SpotifyJson.Queue(body, max)
            : [];

    /// <summary>Bağlı ama belirteçte Ada'nın çalma izni yok (izin eklenmeden önce bağlanılmış): "İzni yenile" gerekir.</summary>
    public bool NeedsReconnect => IsConnected && !_token!.HasScope(SpotifyAuth.PlaybackScope);

    /// <summary>
    /// Ada için: sorguyu arar (sanatçı tam eşleşirse sanatçı, yoksa ilk şarkı, o da yoksa ilk çalma listesi) ve çalar. Etkin
    /// cihaz yoksa Spotify'ı açar ve bilgisayar cihazı görünene kadar en çok 8 sn bekler.
    /// </summary>
    public async Task<(SpotifyPlayOutcome Outcome, string? Title)> PlayAsync(string query, Func<Task> openSpotifyApp)
    {
        if (!IsConnected)
        {
            return (SpotifyPlayOutcome.NotConnected, null);
        }

        if (NeedsReconnect)
        {
            return (SpotifyPlayOutcome.NeedsReconnect, null);
        }

        var search = await SendRawAsync(() => new HttpRequestMessage(HttpMethod.Get,
            $"{Api}/search?q={Uri.EscapeDataString(query)}&type=artist,track,playlist&limit=1"));
        if (search is not { Status: >= 200 and < 300 } found)
        {
            return (Classify(search), null);
        }

        if (SpotifyJson.PickFromSearch(found.Body, query) is not { } pick)
        {
            return (SpotifyPlayOutcome.NotFound, null);
        }

        var play = await SendRawAsync(() => PlayRequest(pick, null));
        if (play is { Status: 404 } && play.Value.Body.Contains("NO_ACTIVE_DEVICE", StringComparison.OrdinalIgnoreCase))
        {
            await openSpotifyApp();
            string? device = null;
            var deadline = _time.GetUtcNow() + TimeSpan.FromSeconds(8);
            while (device is null && _time.GetUtcNow() < deadline)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(500), _time);
                if (await SendRawAsync(() => new HttpRequestMessage(HttpMethod.Get, $"{Api}/me/player/devices")) is { Status: 200 } devices)
                {
                    device = SpotifyJson.PickComputerDevice(devices.Body);
                }
            }

            if (device is null)
            {
                return (SpotifyPlayOutcome.NoDevice, null);
            }

            play = await SendRawAsync(() => PlayRequest(pick, device));
        }

        var outcome = Classify(play);
        return (outcome, outcome == SpotifyPlayOutcome.Playing ? pick.Title : null);
    }

    private static HttpRequestMessage PlayRequest(SpotifyPick pick, string? deviceId)
    {
        var body = pick.ContextUri is { } context
            ? new JsonObject { ["context_uri"] = context }
            : new JsonObject { ["uris"] = new JsonArray(pick.TrackUri) };
        var url = $"{Api}/me/player/play" + (deviceId is null ? "" : "?device_id=" + Uri.EscapeDataString(deviceId));
        return new HttpRequestMessage(HttpMethod.Put, url)
        {
            Content = new StringContent(body.ToJsonString(), System.Text.Encoding.UTF8, "application/json"),
        };
    }

    private static SpotifyPlayOutcome Classify((int Status, string Body)? response) => response switch
    {
        null => SpotifyPlayOutcome.Failed,
        { Status: >= 200 and < 300 } => SpotifyPlayOutcome.Playing,
        { Status: 403 } r when r.Body.Contains("PREMIUM", StringComparison.OrdinalIgnoreCase) => SpotifyPlayOutcome.PremiumRequired,
        { Status: 403 } r when r.Body.Contains("scope", StringComparison.OrdinalIgnoreCase) => SpotifyPlayOutcome.NeedsReconnect,
        { Status: 404 } r when r.Body.Contains("NO_ACTIVE_DEVICE", StringComparison.OrdinalIgnoreCase) => SpotifyPlayOutcome.NoDevice,
        _ => SpotifyPlayOutcome.Failed,
    };

    /// <summary>İsteği gönderir; başarılıysa gövde (boş olabilir), değilse null.</summary>
    private async Task<string?> SendAsync(Func<HttpRequestMessage> request) =>
        await SendRawAsync(request) is { Status: >= 200 and < 300 } response ? response.Body : null;

    /// <summary>
    /// İsteği gönderir (süresi dolmuş belirteci yeniler, 401'de bir kez yeniler, 429'da bekler); yanıtın durum kodu ve gövdesi.
    /// Bağlı değilse, beklemedeyse ya da ağ hatasında null.
    /// </summary>
    private async Task<(int Status, string Body)?> SendRawAsync(Func<HttpRequestMessage> request)
    {
        if (!IsConnected || _time.GetUtcNow() < _blockedUntil)
        {
            return null;
        }

        try
        {
            for (var attempt = 0; attempt < 2; attempt++)
            {
                if (_token!.IsExpired(_time.GetUtcNow()) && !await RefreshAsync())
                {
                    return null;
                }

                using var message = request();
                message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _token!.AccessToken);
                using var response = await _http.SendAsync(message);

                if (response.StatusCode == HttpStatusCode.Unauthorized && attempt == 0)
                {
                    if (!await RefreshAsync())
                    {
                        return null;
                    }

                    continue;
                }

                if (response.StatusCode == HttpStatusCode.TooManyRequests)
                {
                    var wait = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                    _blockedUntil = _time.GetUtcNow() + wait;
                    return null;
                }

                return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            Debug.WriteLine($"Spotify isteği başarısız: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Belirteci yeniler; aynı anda gelen istekler tek yenilemeyi bekler (Spotify yenileme belirtecini değiştirebilir, eskisiyle
    /// ikinci bir yenileme reddedilip bağlantıyı koparırdı). Spotify yenilemeyi reddederse (400/401) bağlantı kopar.
    /// </summary>
    private Task<bool> RefreshAsync()
    {
        if (_refreshing is { IsCompleted: false } running)
        {
            return running;
        }

        return _refreshing = RefreshOnceAsync();
    }

    private async Task<bool> RefreshOnceAsync()
    {
        if (_token is null || _clientId() is not { Length: > 0 } clientId)
        {
            return false;
        }

        var refreshed = await RequestTokenAsync(SpotifyAuth.RefreshForm(clientId, _token.RefreshToken), _token.RefreshToken, _token.Scope);
        if (refreshed is null)
        {
            return false;
        }

        SetToken(refreshed);
        return true;
    }

    private async Task<SpotifyToken?> RequestTokenAsync(
        IReadOnlyDictionary<string, string> form, string? previousRefreshToken, string? previousScope = null)
    {
        try
        {
            using var content = new FormUrlEncodedContent(form);
            using var response = await _http.PostAsync("https://accounts.spotify.com/api/token", content);
            if (response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Unauthorized && previousRefreshToken is not null)
            {
                Disconnect(); // yenileme belirteci geçersiz: yeniden bağlanmak gerekir
                return null;
            }

            return response.IsSuccessStatusCode
                ? SpotifyToken.Parse(await response.Content.ReadAsStringAsync(), _time.GetUtcNow(), previousRefreshToken, previousScope)
                : null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            Debug.WriteLine($"Spotify belirteci alınamadı: {ex.Message}");
            return null;
        }
    }

    private void SetToken(SpotifyToken token)
    {
        var wasConnected = _token is not null;
        _token = token;
        _store.Save(token);
        if (!wasConnected)
        {
            ConnectionChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    private void Disconnect()
    {
        _token = null;
        _store.Clear();
        ConnectionChanged?.Invoke(this, EventArgs.Empty);
    }

    private static HttpClient CreateClient(HttpMessageHandler handler)
    {
        var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("CamAda");
        return client;
    }
}

/// <summary>Ada'nın "Spotify'da çal" isteğinin sonucu.</summary>
public enum SpotifyPlayOutcome
{
    Playing,
    NotConnected,
    NeedsReconnect,
    NoDevice,
    NotFound,
    PremiumRequired,
    Failed,
}
