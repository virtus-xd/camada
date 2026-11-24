namespace CamAda.Core.Media;

public enum PlaybackState
{
    Stopped,
    Paused,
    Playing,
}

/// <summary>Windows medya oturumundan okunan çalan parça (kapak hariç; kapak WPF katmanında tutulur).</summary>
/// <param name="SourceAppId">Oturumun kaynak AppUserModelId'si (ör. "Spotify.exe").</param>
/// <param name="PositionUpdatedAt">Position'ın ölçüldüğü an; çalarken konum buradan ileri hesaplanır.</param>
public sealed record NowPlaying(
    string SourceAppId,
    string Title,
    string Artist,
    string Album,
    PlaybackState State,
    TimeSpan Position,
    TimeSpan Duration,
    DateTimeOffset PositionUpdatedAt);

/// <summary>Medya oturumları arasından gösterilecek olanı seçer (spec §1: yalnız Spotify).</summary>
public static class MediaSourcePicker
{
    /// <summary>
    /// Kaynak kimliği "spotify" içeren ilk oturumun sırası; yoksa -1. Windows'un "geçerli" oturumu başka bir uygulama
    /// (tarayıcıda video) olsa bile Spotify seçilir.
    /// </summary>
    public static int PickSpotify(IReadOnlyList<string> sourceAppIds)
    {
        for (var i = 0; i < sourceAppIds.Count; i++)
        {
            if (sourceAppIds[i].Contains("spotify", StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
