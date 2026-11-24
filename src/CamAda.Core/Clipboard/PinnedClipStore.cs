using System.Text.Json;

namespace CamAda.Core.Clipboard;

/// <summary>
/// Sabitlenen pano öğelerinin kaydı (tasarım "Saklama"): <c>pinned.json</c> (metin ve biçimleri) + görseller için
/// <c>&lt;id&gt;.png</c>. Sabitlenmemiş öğeler buraya hiç gelmez. G/Ç hataları çağırana bırakılır.
/// </summary>
public sealed class PinnedClipStore(string directory)
{
    private const string IndexName = "pinned.json";
    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public IReadOnlyList<ClipItem> Load()
    {
        var index = Path.Combine(directory, IndexName);
        if (!File.Exists(index))
        {
            return [];
        }

        List<Entry>? entries;
        try
        {
            entries = JsonSerializer.Deserialize<List<Entry>>(File.ReadAllText(index), Json);
        }
        catch (JsonException)
        {
            return [];
        }

        var items = new List<ClipItem>();
        foreach (var e in entries ?? [])
        {
            ClipPayload payload;
            if (e.Image is { } image)
            {
                var path = Path.Combine(directory, image);
                if (!File.Exists(path))
                {
                    continue;
                }

                payload = ClipPayload.FromImage(File.ReadAllBytes(path), e.Width, e.Height, e.PixelHash ?? e.Id);
            }
            else if (e.Text is { } text)
            {
                payload = ClipPayload.FromText(text, e.Html, e.Rtf);
            }
            else
            {
                continue;
            }

            items.Add(new ClipItem(e.Id, e.Kind, payload, e.CopiedAt, Pinned: true));
        }

        return items;
    }

    public void Save(IReadOnlyList<ClipItem> pinned)
    {
        Directory.CreateDirectory(directory);
        var entries = new List<Entry>();
        var keep = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in pinned)
        {
            string? image = null;
            if (item.Payload.Png is { } png)
            {
                image = item.Id + ".png";
                keep.Add(image);
                var path = Path.Combine(directory, image);
                if (!File.Exists(path))
                {
                    WriteAtomically(path, tmp => File.WriteAllBytes(tmp, png));
                }
            }

            entries.Add(new Entry(item.Id, item.Kind, item.CopiedAt, item.Payload.Text, item.Payload.Html, item.Payload.Rtf,
                image, item.Payload.Width, item.Payload.Height, item.Payload.PixelHash));
        }

        WriteAtomically(Path.Combine(directory, IndexName), tmp => File.WriteAllText(tmp, JsonSerializer.Serialize(entries, Json)));

        foreach (var png in Directory.EnumerateFiles(directory, "*.png"))
        {
            if (!keep.Contains(Path.GetFileName(png)))
            {
                File.Delete(png);
            }
        }
    }

    private static void WriteAtomically(string path, Action<string> write)
    {
        var tmp = path + ".tmp";
        write(tmp);
        File.Move(tmp, path, overwrite: true);
    }

    private sealed record Entry(string Id, ClipKind Kind, DateTimeOffset CopiedAt, string? Text, string? Html, string? Rtf,
        string? Image, int Width, int Height, string? PixelHash);
}
