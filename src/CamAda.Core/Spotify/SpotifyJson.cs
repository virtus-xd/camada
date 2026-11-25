using System.Text.Json;

namespace CamAda.Core.Spotify;

/// <summary>Ada'nın çalacağı şey: sanatçı ya da çalma listesi bağlamı (<see cref="ContextUri"/>) ya da tek şarkı.</summary>
public sealed record SpotifyPick(string? ContextUri, string? TrackUri, string Title);

/// <summary>Spotify kuyruğundaki ("Sıradaki") bir şarkı.</summary>
public sealed record QueueItem(string Title, string Artist);

/// <summary>
/// Spotify Web API yanıtları (Şubat 2026 geliştirici modu): GET /me/player/currently-playing, GET /me/library/contains,
/// GET /me/player/queue. Bozuk ya da beklenmeyen yanıtlar istisna fırlatmaz: null ya da boş liste döner.
/// </summary>
public static class SpotifyJson
{
    /// <summary>Çalan parça şarkıysa adresi (spotify:track:…) ve adı; hiçbir şey çalmıyorsa ya da podcast bölümüyse null.</summary>
    public static (string Uri, string Title)? CurrentTrack(string json) =>
        Read(json, root =>
            root.ValueKind == JsonValueKind.Object && root.TryGetProperty("item", out var item)
            && Text(item, "type") == "track"
            && Text(item, "uri") is { Length: > 0 } trackUri
            && Text(item, "name") is { } title
                ? (trackUri, title)
                : ((string, string)?)null);

    /// <summary>"[true]" / "[false]" → ilk değer; bozuksa null.</summary>
    public static bool? Contains(string json) =>
        Read(json, root =>
            root.ValueKind == JsonValueKind.Array && root.GetArrayLength() > 0
            && root[0].ValueKind is JsonValueKind.True or JsonValueKind.False
                ? root[0].GetBoolean()
                : (bool?)null);

    /// <summary>Sıradaki en fazla <paramref name="max"/> şarkı; sanatçılar ", " ile birleştirilir.</summary>
    public static IReadOnlyList<QueueItem> Queue(string json, int max) =>
        Read(json, root =>
        {
            if (root.ValueKind != JsonValueKind.Object
                || !root.TryGetProperty("queue", out var queue) || queue.ValueKind != JsonValueKind.Array)
            {
                return [];
            }

            return queue.EnumerateArray()
                .Where(t => Text(t, "name") is not null)
                .Take(max)
                .Select(t => new QueueItem(
                    Text(t, "name")!,
                    t.TryGetProperty("artists", out var artists) && artists.ValueKind == JsonValueKind.Array
                        ? string.Join(", ", artists.EnumerateArray()
                            .Select(a => Text(a, "name"))
                            .Where(n => !string.IsNullOrEmpty(n)))
                        : ""))
                .ToList();
        }) ?? [];

    /// <summary>
    /// GET /search?type=artist,track,playlist yanıtından çalınacak olanı seçer: adı sorguya tam eşit (tr-TR, harf büyüklüğü
    /// yok sayılır) sanatçı; yoksa ilk şarkı; o da yoksa ilk çalma listesi. Hiçbiri yoksa null.
    /// </summary>
    public static SpotifyPick? PickFromSearch(string json, string query) =>
        Read(json, root =>
        {
            var turkish = System.Globalization.CultureInfo.GetCultureInfo("tr-TR");
            var wanted = query.Trim().ToLower(turkish);
            var artist = Items(root, "artists").FirstOrDefault(a => Text(a, "name")?.Trim().ToLower(turkish) == wanted);
            if (artist.ValueKind == JsonValueKind.Object && Text(artist, "uri") is { Length: > 0 } artistUri)
            {
                return new SpotifyPick(artistUri, null, Text(artist, "name")!);
            }

            var track = Items(root, "tracks").FirstOrDefault(t => Text(t, "uri") is { Length: > 0 } && Text(t, "name") is not null);
            if (track.ValueKind == JsonValueKind.Object)
            {
                var artists = track.TryGetProperty("artists", out var list) && list.ValueKind == JsonValueKind.Array
                    ? string.Join(", ", list.EnumerateArray().Select(a => Text(a, "name")).Where(n => !string.IsNullOrEmpty(n)))
                    : "";
                var name = Text(track, "name")!;
                return new SpotifyPick(null, Text(track, "uri"), artists.Length > 0 ? $"{artists} — {name}" : name);
            }

            var playlist = Items(root, "playlists").FirstOrDefault(p => Text(p, "uri") is { Length: > 0 } && Text(p, "name") is not null);
            return playlist.ValueKind == JsonValueKind.Object ? new SpotifyPick(Text(playlist, "uri"), null, Text(playlist, "name")!) : null;
        });

    /// <summary>GET /me/player/devices yanıtındaki bilgisayar cihazının kimliği ("type": "Computer"); yoksa null.</summary>
    public static string? PickComputerDevice(string devicesJson) =>
        Read(devicesJson, root => Items(root, "devices", direct: true)
            .Where(d => Text(d, "type") == "Computer")
            .Select(d => Text(d, "id"))
            .FirstOrDefault(id => !string.IsNullOrEmpty(id)));

    /// <summary>"alan.items" dizisindeki nesneler (null öğeler atlanır); <paramref name="direct"/> ise doğrudan "alan" dizisi.</summary>
    private static IEnumerable<JsonElement> Items(JsonElement root, string property, bool direct = false)
    {
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty(property, out var value))
        {
            return [];
        }

        if (!direct)
        {
            if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty("items", out value))
            {
                return [];
            }
        }

        return value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.Object).ToList()
            : [];
    }

    /// <summary>Nesnenin metin türündeki alanı; nesne değilse, alan yoksa ya da metin değilse null.</summary>
    private static string? Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static T? Read<T>(string json, Func<JsonElement, T?> read)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return default;
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.ValueKind is JsonValueKind.Object or JsonValueKind.Array ? read(doc.RootElement) : default;
        }
        catch (Exception ex) when (ex is JsonException or InvalidOperationException) // bozuk ya da beklenmedik türde yanıt
        {
            return default;
        }
    }
}
