using CamAda.Core.Spotify;

namespace CamAda.Media;

/// <summary>
/// Spotify Web API'den gelen ekler (spec §5, §7.4 "Müzik"): beğen ve sıradaki şarkılar. Bağlı değilken ya da son istek
/// başarısızken <see cref="IsAvailable"/> false olur; kart ve panel bu durumda ilgili denetimleri gizler.
/// </summary>
public interface ISpotifyExtras
{
    /// <summary>Bağlı ve son istek başarılı.</summary>
    bool IsAvailable { get; }

    /// <summary>Çalan şarkı beğenilenlerde mi; bilinmiyorsa null (kalp gizli).</summary>
    bool? IsCurrentSaved { get; }

    IReadOnlyList<QueueItem> UpNext { get; }

    /// <summary>Durum değişince (arayüz iş parçacığında).</summary>
    event EventHandler? Changed;

    Task ToggleSavedAsync();

    /// <summary>Kart ya da panel açılınca güncel bilgiyi ister.</summary>
    Task RefreshAsync();
}
