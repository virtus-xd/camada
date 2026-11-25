using CamAda.Core.Spotify;

namespace CamAda.Core.Tests.Spotify;

public class SpotifyFooterTests
{
    [Theory]
    [InlineData(false, false, false, SpotifyFooterKind.Connect)]
    [InlineData(false, false, true, SpotifyFooterKind.Connect)]
    [InlineData(true, true, true, SpotifyFooterKind.None)]
    [InlineData(true, false, false, SpotifyFooterKind.None)]        // Ada kapalı: eski izin yeterli, "İzni yenile" yok
    [InlineData(true, false, true, SpotifyFooterKind.RenewForAda)]
    public void Footer(bool connected, bool hasPlaybackScope, bool assistantEnabled, SpotifyFooterKind expected) =>
        Assert.Equal(expected, SpotifyFooter.For(connected, hasPlaybackScope, assistantEnabled));
}
