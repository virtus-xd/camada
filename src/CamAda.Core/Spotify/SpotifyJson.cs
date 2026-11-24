using System.Text.Json;

namespace CamAda.Core.Spotify;

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
