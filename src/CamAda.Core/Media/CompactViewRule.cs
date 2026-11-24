namespace CamAda.Core.Media;

/// <summary>Küçük adanın gösterdiği içerik (spec §7.1).</summary>
public enum CompactView
{
    /// <summary>Tarih, hava durumu, saat.</summary>
    Idle,

    /// <summary>Mini plak, şarkı ve sanatçı, ekolayzır, saat.</summary>
    Music,
}

public static class CompactViewRule
{
    /// <summary>Şarkı geçişlerinde oturum kısa bir an "duraklamış" görünür; ada bu sürede boşta hâline atlamaz.</summary>
    public static readonly TimeSpan Grace = TimeSpan.FromSeconds(3);

    /// <summary>Çalıyorsa müzik; duraklamış ya da durmuşsa son çalmadan beri Grace dolmadıysa müzik, sonra boşta.</summary>
    public static CompactView Decide(NowPlaying? track, DateTimeOffset? lastPlayingAt, DateTimeOffset now)
    {
        if (track is null)
        {
            return CompactView.Idle;
        }

        if (track.State == PlaybackState.Playing)
        {
            return CompactView.Music;
        }

        return lastPlayingAt is { } last && now - last < Grace ? CompactView.Music : CompactView.Idle;
    }
}
