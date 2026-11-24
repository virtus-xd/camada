using CamAda.Core.Media;

namespace CamAda.Core.Tests.Media;

public class MediaTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 24, 12, 0, 0, TimeSpan.Zero);

    private static NowPlaying Track(PlaybackState state = PlaybackState.Playing, double position = 10, double duration = 200) =>
        new("Spotify.exe", "Şarkı", "Sanatçı", "Albüm", state,
            TimeSpan.FromSeconds(position), TimeSpan.FromSeconds(duration), T0);

    // --- MediaSourcePicker ---

    [Fact]
    public void Spotify_is_picked_even_when_not_first() =>
        Assert.Equal(1, MediaSourcePicker.PickSpotify(["Chrome", "Spotify.exe"]));

    [Fact]
    public void Store_spotify_is_picked() =>
        Assert.Equal(0, MediaSourcePicker.PickSpotify(["SpotifyAB.SpotifyMusic_zpdnekdrzrea0!Spotify"]));

    [Fact]
    public void Other_players_are_not_picked() =>
        Assert.Equal(-1, MediaSourcePicker.PickSpotify(["msedge.exe"]));

    [Fact]
    public void No_sessions_picks_nothing() =>
        Assert.Equal(-1, MediaSourcePicker.PickSpotify([]));

    // --- MediaTimeline ---

    [Fact]
    public void Playing_position_moves_with_time() =>
        Assert.Equal(TimeSpan.FromSeconds(12.5), MediaTimeline.PositionAt(Track(), T0.AddSeconds(2.5)));

    [Fact]
    public void Paused_position_stays() =>
        Assert.Equal(TimeSpan.FromSeconds(10), MediaTimeline.PositionAt(Track(PlaybackState.Paused), T0.AddSeconds(30)));

    [Fact]
    public void Position_is_clamped_to_duration() =>
        Assert.Equal(TimeSpan.FromSeconds(60), MediaTimeline.PositionAt(Track(duration: 60), T0.AddSeconds(90)));

    [Fact]
    public void Unknown_duration_does_not_clamp() =>
        Assert.Equal(TimeSpan.FromSeconds(100), MediaTimeline.PositionAt(Track(duration: 0), T0.AddSeconds(90)));

    [Theory]
    [InlineData(7, "0:07")]
    [InlineData(187, "3:07")]
    [InlineData(3723, "1:02:03")]
    public void Times_are_formatted(double seconds, string expected) =>
        Assert.Equal(expected, MediaTimeline.Format(TimeSpan.FromSeconds(seconds)));

    [Fact]
    public void Unknown_duration_has_no_progress() =>
        Assert.Null(MediaTimeline.Progress(Track(duration: 0), T0));

    [Fact]
    public void Progress_is_fraction_of_duration() =>
        Assert.Equal(0.5, MediaTimeline.Progress(Track(PlaybackState.Paused, position: 100, duration: 200), T0));

    // --- CompactViewRule ---

    [Fact]
    public void No_track_is_idle() =>
        Assert.Equal(CompactView.Idle, CompactViewRule.Decide(null, T0, T0));

    [Fact]
    public void Playing_shows_music() =>
        Assert.Equal(CompactView.Music, CompactViewRule.Decide(Track(), T0, T0));

    [Fact]
    public void Short_pause_keeps_music() =>
        Assert.Equal(CompactView.Music, CompactViewRule.Decide(Track(PlaybackState.Paused), T0, T0.AddSeconds(2)));

    [Fact]
    public void Long_pause_goes_idle() =>
        Assert.Equal(CompactView.Idle, CompactViewRule.Decide(Track(PlaybackState.Paused), T0, T0.AddSeconds(4)));

    [Fact]
    public void Paused_without_any_playing_is_idle() =>
        Assert.Equal(CompactView.Idle, CompactViewRule.Decide(Track(PlaybackState.Paused), null, T0));

    // --- CoverRule ---

    [Fact]
    public void New_track_loads_cover() =>
        Assert.True(CoverRule.ShouldLoad("b", lastKey: "a", lastHadCover: true, hasThumbnail: true));

    [Fact]
    public void Same_track_with_cover_is_not_reloaded() =>
        Assert.False(CoverRule.ShouldLoad("a", lastKey: "a", lastHadCover: true, hasThumbnail: true));

    // Oturum önce başlığı, biraz sonra küçük resmi bildirir: aynı parçada gelen küçük resim yine okunmalı.
    [Fact]
    public void Same_track_loads_cover_that_arrives_later() =>
        Assert.True(CoverRule.ShouldLoad("a", lastKey: "a", lastHadCover: false, hasThumbnail: true));

    [Fact]
    public void Same_track_without_thumbnail_is_not_reloaded() =>
        Assert.False(CoverRule.ShouldLoad("a", lastKey: "a", lastHadCover: false, hasThumbnail: false));

    // --- DominantColor ---

    private static byte[] Pixels(params (byte R, byte G, byte B, byte A, int Count)[] runs)
    {
        var list = new List<byte>();
        foreach (var (r, g, b, a, count) in runs)
        {
            for (var i = 0; i < count; i++)
            {
                list.AddRange([b, g, r, a]);
            }
        }

        return list.ToArray();
    }

    [Fact]
    public void Empty_image_falls_back() =>
        Assert.Equal(DominantColor.Fallback, DominantColor.From([]));

    [Fact]
    public void Transparent_image_falls_back() =>
        Assert.Equal(DominantColor.Fallback, DominantColor.From(Pixels((200, 30, 30, 0, 100))));

    [Fact]
    public void Black_image_falls_back() =>
        Assert.Equal(DominantColor.Fallback, DominantColor.From(Pixels((0, 0, 0, 255, 100))));

    [Fact]
    public void Gray_image_falls_back() =>
        Assert.Equal(DominantColor.Fallback, DominantColor.From(Pixels((128, 128, 128, 255, 60), (200, 200, 200, 255, 40))));

    [Fact]
    public void Most_common_hue_wins()
    {
        var color = DominantColor.From(Pixels((0xD0, 0x20, 0x20, 255, 70), (0x20, 0x50, 0xD0, 255, 30)));
        Assert.True(color.R > 180 && color.G < 60 && color.B < 60, $"{color}");
    }
}
