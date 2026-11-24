using System.Globalization;

namespace CamAda.Core.Media;

/// <summary>Parçanın o anki konumu, ilerleme oranı ve süre metinleri.</summary>
public static class MediaTimeline
{
    /// <summary>
    /// Çalıyorsa ölçülen konum + geçen süre, değilse ölçülen konum; 0 ile süre arasına kırpılır (süre bilinmiyorsa üst sınır yok).
    /// Medya oturumu konumu yalnız arada bir bildirir; arayüz bu yüzden konumu kendisi ilerletir.
    /// </summary>
    public static TimeSpan PositionAt(NowPlaying track, DateTimeOffset now)
    {
        var position = track.State == PlaybackState.Playing
            ? track.Position + (now - track.PositionUpdatedAt)
            : track.Position;

        if (position < TimeSpan.Zero)
        {
            return TimeSpan.Zero;
        }

        return track.Duration > TimeSpan.Zero && position > track.Duration ? track.Duration : position;
    }

    /// <summary>"0:07", "3:07", "1:02:03".</summary>
    public static string Format(TimeSpan t)
    {
        var total = (long)Math.Max(0, Math.Floor(t.TotalSeconds));
        var hours = total / 3600;
        var minutes = total % 3600 / 60;
        var seconds = total % 60;
        return hours > 0
            ? string.Create(CultureInfo.InvariantCulture, $"{hours}:{minutes:00}:{seconds:00}")
            : string.Create(CultureInfo.InvariantCulture, $"{minutes}:{seconds:00}");
    }

    /// <summary>0..1 ilerleme; süre bilinmiyorsa null (ilerleme çubuğu gizlenir).</summary>
    public static double? Progress(NowPlaying track, DateTimeOffset now) =>
        track.Duration > TimeSpan.Zero ? PositionAt(track, now) / track.Duration : null;
}
