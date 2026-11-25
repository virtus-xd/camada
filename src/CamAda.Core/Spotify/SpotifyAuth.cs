using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace CamAda.Core.Spotify;

/// <summary>
/// Spotify'a PKCE ile giriş (spec §5, §8): kullanıcının kendi Client ID'si, gizli anahtar yok. Yönlendirme yerel geri döngü
/// adresine (127.0.0.1; Spotify "localhost"u kabul etmiyor) gelir. Kapsamlar yalnız beğen ve sıradaki şarkılar için.
/// </summary>
public static class SpotifyAuth
{
    public const int CallbackPort = 43821;
    public const string RedirectUri = "http://127.0.0.1:43821/callback";
    public const string Scopes = "user-library-read user-library-modify user-read-playback-state user-read-currently-playing";

    private const string Unreserved = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789-._~";

    /// <summary>64 karakterlik rastgele kod doğrulayıcı (RFC 7636 "unreserved" karakterleri).</summary>
    public static string CreateVerifier() => RandomString(64);

    /// <summary>Girişin bu uygulamadan başladığını doğrulayan 32 karakterlik rastgele durum.</summary>
    public static string CreateState() => RandomString(32);

    /// <summary>S256: base64url(SHA-256(ASCII(verifier))), dolgusuz.</summary>
    public static string ChallengeFor(string verifier)
    {
        var hash = SHA256.HashData(Encoding.ASCII.GetBytes(verifier));
        return Convert.ToBase64String(hash).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    public static string AuthorizeUrl(string clientId, string challenge, string state) =>
        "https://accounts.spotify.com/authorize?" + Query(
            ("client_id", clientId),
            ("response_type", "code"),
            ("redirect_uri", RedirectUri),
            ("code_challenge_method", "S256"),
            ("code_challenge", challenge),
            ("scope", Scopes),
            ("state", state));

    /// <summary>https://accounts.spotify.com/api/token için yetki kodu takası gövdesi.</summary>
    public static IReadOnlyDictionary<string, string> CodeExchangeForm(string clientId, string code, string verifier) =>
        new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["code"] = code,
            ["redirect_uri"] = RedirectUri,
            ["client_id"] = clientId,
            ["code_verifier"] = verifier,
        };

    public static IReadOnlyDictionary<string, string> RefreshForm(string clientId, string refreshToken) =>
        new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
        };

    private static string RandomString(int length) => new(RandomNumberGenerator.GetItems<char>(Unreserved, length));

    private static string Query(params (string Key, string Value)[] pairs) =>
        string.Join("&", pairs.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"));
}

/// <summary>Erişim belirteci ve yenileme belirteci; erişim belirteci yaklaşık bir saat geçerli.</summary>
public sealed record SpotifyToken(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAt)
{
    /// <summary>Bitişe bir dakikadan az kaldıysa süresi dolmuş sayılır (istek yoldayken dolmasın).</summary>
    public bool IsExpired(DateTimeOffset now) => now >= ExpiresAt - TimeSpan.FromSeconds(60);

    /// <summary>
    /// Belirteç yanıtı: access_token ve expires_in zorunlu; yenilemede refresh_token gelmeyebilir, o zaman öncekisi kalır.
    /// Eksik ya da bozuk yanıtta (ya da hiç yenileme belirteci yoksa) null.
    /// </summary>
    public static SpotifyToken? Parse(string json, DateTimeOffset now, string? previousRefreshToken)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("access_token", out var access) || access.ValueKind != JsonValueKind.String
                || access.GetString() is not { Length: > 0 } accessToken
                || !root.TryGetProperty("expires_in", out var expires) || !expires.TryGetInt32(out var seconds))
            {
                return null;
            }

            var refreshToken = root.TryGetProperty("refresh_token", out var refresh) && refresh.ValueKind == JsonValueKind.String
                               && refresh.GetString() is { Length: > 0 } r
                ? r
                : previousRefreshToken;
            return refreshToken is null ? null : new SpotifyToken(accessToken, refreshToken, now.AddSeconds(seconds));
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) // bozuk ya da beklenmedik türde yanıt
        {
            return null;
        }
    }
}

public sealed record OAuthCallbackResult(string? Code, string? State, string? Error);

/// <summary>Geri dönüşün bu girişe göre anlamı.</summary>
public enum CallbackVerdict
{
    /// <summary>Durum eşleşti, kod var: takas edilebilir.</summary>
    Accept,

    /// <summary>Durum eşleşti ama kullanıcı reddetti ya da yanıt eksik: giriş başarısız biter.</summary>
    Denied,

    /// <summary>Başka (eski) bir girişin geri dönüşü: yok sayılır, bekleme sürer.</summary>
    Stale,
}

/// <summary>Tarayıcının yerel geri dönüş adresine yaptığı isteğin ilk satırı: "GET /callback?code=..&amp;state=.. HTTP/1.1".</summary>
public static class OAuthCallback
{
    /// <summary>
    /// Durum (state) bu girişinkiyle eşleşmeyen geri dönüş eskidir ya da sahtedir: beklemeyi bitirmez. Eşleşen yanıtta kod
    /// yoksa (ret ya da eksik yanıt) giriş başarısız biter.
    /// </summary>
    public static CallbackVerdict Judge(OAuthCallbackResult result, string expectedState) =>
        !string.Equals(result.State, expectedState, StringComparison.Ordinal) ? CallbackVerdict.Stale
        : result.Error is null && !string.IsNullOrEmpty(result.Code) ? CallbackVerdict.Accept
        : CallbackVerdict.Denied;

    public static OAuthCallbackResult? Parse(string requestLine)
    {
        var parts = requestLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 2 || parts[0] != "GET")
        {
            return null;
        }

        var target = parts[1];
        var question = target.IndexOf('?');
        var path = question < 0 ? target : target[..question];
        if (path != "/callback")
        {
            return null;
        }

        var values = new Dictionary<string, string>();
        if (question >= 0)
        {
            foreach (var pair in target[(question + 1)..].Split('&', StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = pair.IndexOf('=');
                var key = Uri.UnescapeDataString(eq < 0 ? pair : pair[..eq]);
                values[key] = eq < 0 ? "" : Uri.UnescapeDataString(pair[(eq + 1)..].Replace('+', ' '));
            }
        }

        return new OAuthCallbackResult(
            values.GetValueOrDefault("code"), values.GetValueOrDefault("state"), values.GetValueOrDefault("error"));
    }
}
