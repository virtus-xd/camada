namespace CamAda.Core.Spotify;

public enum SpotifyFooterKind
{
    /// <summary>Müzik panelinin altında Spotify satırı yok.</summary>
    None,

    /// <summary>"Spotify'a bağlan" (beğen ve sıradakiler için).</summary>
    Connect,

    /// <summary>"İzni yenile": bağlantı Ada'nın çalma izninden önce alınmış ve Ada açık.</summary>
    RenewForAda,
}

public static class SpotifyFooter
{
    /// <summary>Ada kapalıyken eski izin beğen ve sıradakiler için yeterlidir: yenileme istenmez.</summary>
    public static SpotifyFooterKind For(bool connected, bool hasPlaybackScope, bool assistantEnabled) =>
        !connected ? SpotifyFooterKind.Connect
        : !hasPlaybackScope && assistantEnabled ? SpotifyFooterKind.RenewForAda
        : SpotifyFooterKind.None;
}
