using CamAda.Core.Spotify;

namespace CamAda.Core.Tests.Spotify;

public class SpotifyTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    // --- PKCE ---

    [Fact]
    public void Verifier_is_64_allowed_characters()
    {
        var verifier = SpotifyAuth.CreateVerifier();
        Assert.Equal(64, verifier.Length);
        Assert.All(verifier, c => Assert.True(char.IsAsciiLetterOrDigit(c) || c is '-' or '.' or '_' or '~', $"'{c}'"));
    }

    [Fact]
    public void Verifiers_are_random() =>
        Assert.NotEqual(SpotifyAuth.CreateVerifier(), SpotifyAuth.CreateVerifier());

    // RFC 7636, Ek B.
    [Fact]
    public void Challenge_matches_rfc_example() =>
        Assert.Equal("E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM",
            SpotifyAuth.ChallengeFor("dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk"));

    [Fact]
    public void Authorize_url_has_pkce_parameters()
    {
        var url = SpotifyAuth.AuthorizeUrl("istemci", "meydan", "durum");
        Assert.StartsWith("https://accounts.spotify.com/authorize?", url);
        Assert.Contains("client_id=istemci", url);
        Assert.Contains("response_type=code", url);
        Assert.Contains("code_challenge_method=S256", url);
        Assert.Contains("code_challenge=meydan", url);
        Assert.Contains("state=durum", url);
        Assert.Contains("redirect_uri=http%3A%2F%2F127.0.0.1%3A43821%2Fcallback", url);
        Assert.Contains("scope=user-library-read%20user-library-modify%20user-read-playback-state%20user-read-currently-playing", url);
    }

    [Fact]
    public void Code_exchange_form_carries_the_verifier()
    {
        var form = SpotifyAuth.CodeExchangeForm("istemci", "kod", "doğrulayıcı");
        Assert.Equal("authorization_code", form["grant_type"]);
        Assert.Equal("kod", form["code"]);
        Assert.Equal(SpotifyAuth.RedirectUri, form["redirect_uri"]);
        Assert.Equal("istemci", form["client_id"]);
        Assert.Equal("doğrulayıcı", form["code_verifier"]);
    }

    [Fact]
    public void Refresh_form_uses_refresh_token()
    {
        var form = SpotifyAuth.RefreshForm("istemci", "yenile");
        Assert.Equal("refresh_token", form["grant_type"]);
        Assert.Equal("yenile", form["refresh_token"]);
        Assert.Equal("istemci", form["client_id"]);
    }

    // --- Token ---

    [Fact]
    public void Token_expiry_is_computed_from_expires_in()
    {
        var token = SpotifyToken.Parse("""{"access_token":"A","token_type":"Bearer","expires_in":3600,"refresh_token":"R"}""", Now, null);
        Assert.Equal(new SpotifyToken("A", "R", Now.AddHours(1)), token);
    }

    [Fact]
    public void Refresh_without_new_refresh_token_keeps_the_old_one() =>
        Assert.Equal("ESKİ", SpotifyToken.Parse("""{"access_token":"A","expires_in":3600}""", Now, "ESKİ")!.RefreshToken);

    [Fact]
    public void Token_is_expired_a_minute_before_its_end()
    {
        var token = new SpotifyToken("A", "R", Now.AddHours(1));
        Assert.True(token.IsExpired(Now.AddHours(1).AddSeconds(-59)));
        Assert.False(token.IsExpired(Now.AddHours(1).AddSeconds(-61)));
    }

    [Fact]
    public void Broken_token_response_is_null()
    {
        Assert.Null(SpotifyToken.Parse("<html>", Now, null));
        Assert.Null(SpotifyToken.Parse("""{"expires_in":3600}""", Now, null));
        Assert.Null(SpotifyToken.Parse("""{"access_token":"A","expires_in":3600}""", Now, null)); // yenileme belirteci hiç yok
    }

    // --- Yerel geri dönüş isteği ---

    [Fact]
    public void Callback_with_code_is_parsed() =>
        Assert.Equal(new OAuthCallbackResult("abc", "xyz", null),
            OAuthCallback.Parse("GET /callback?code=abc&state=xyz HTTP/1.1"));

    [Fact]
    public void Callback_with_error_is_parsed() =>
        Assert.Equal(new OAuthCallbackResult(null, "xyz", "access_denied"),
            OAuthCallback.Parse("GET /callback?error=access_denied&state=xyz HTTP/1.1"));

    [Fact]
    public void Other_requests_are_ignored() =>
        Assert.Null(OAuthCallback.Parse("GET /favicon.ico HTTP/1.1"));

    // --- Yanıtlar ---

    private const string CurrentlyPlaying = """
        {"timestamp":1,"is_playing":true,"currently_playing_type":"track",
         "item":{"type":"track","id":"4uLU6hMCjMI75M1A2tKUQC","uri":"spotify:track:4uLU6hMCjMI75M1A2tKUQC","name":"Şarkı",
                 "artists":[{"name":"Sanatçı"}]}}
        """;

    [Fact]
    public void Current_track_uri_and_title_are_read() =>
        Assert.Equal(("spotify:track:4uLU6hMCjMI75M1A2tKUQC", "Şarkı"), SpotifyJson.CurrentTrack(CurrentlyPlaying));

    [Fact]
    public void Nothing_or_episode_playing_has_no_track()
    {
        Assert.Null(SpotifyJson.CurrentTrack("""{"is_playing":false,"item":null}"""));
        Assert.Null(SpotifyJson.CurrentTrack("""{"item":{"type":"episode","uri":"spotify:episode:1","name":"Bölüm"}}"""));
        Assert.Null(SpotifyJson.CurrentTrack(""));
    }

    [Fact]
    public void Library_contains_is_read()
    {
        Assert.True(SpotifyJson.Contains("[true]"));
        Assert.False(SpotifyJson.Contains("[false]"));
        Assert.Null(SpotifyJson.Contains("x"));
    }

    [Fact]
    public void Queue_is_read_up_to_max()
    {
        const string json = """
            {"currently_playing":{"name":"Şimdi"},
             "queue":[{"type":"track","name":"Bir","artists":[{"name":"A"},{"name":"B"}]},
                      {"type":"track","name":"İki","artists":[{"name":"C"}]},
                      {"type":"track","name":"Üç","artists":[]}]}
            """;
        Assert.Equal([new QueueItem("Bir", "A, B"), new QueueItem("İki", "C")], SpotifyJson.Queue(json, 2));
    }

    [Fact]
    public void Broken_queue_is_empty() =>
        Assert.Empty(SpotifyJson.Queue("{", 3));

    // Son inceleme: beklenmedik biçimde JsonElement erişimleri InvalidOperationException atıyordu; yalnız JsonException
    // yakalandığından görev çubuğunun yerini alan uygulama çökebilirdi.
    [Fact]
    public void Unexpected_json_shapes_do_not_throw()
    {
        Assert.Null(SpotifyJson.CurrentTrack("[1]"));
        Assert.Null(SpotifyJson.CurrentTrack("""{"item":{"type":"track","uri":5,"name":"Şarkı"}}"""));
        Assert.Empty(SpotifyJson.Queue("[]", 3));
        Assert.Empty(SpotifyJson.Queue("""{"queue":[null]}""", 3));
        Assert.Empty(SpotifyJson.Queue("""{"queue":[{"name":7}]}""", 3));
        Assert.Equal([new QueueItem("Bir", "")], SpotifyJson.Queue("""{"queue":[{"name":"Bir","artists":[null,{"name":1}]}]}""", 3));
        Assert.Null(SpotifyToken.Parse("""{"access_token":5,"expires_in":3600,"refresh_token":"R"}""", Now, null));
        Assert.Null(SpotifyToken.Parse("[]", Now, null));
    }
}
