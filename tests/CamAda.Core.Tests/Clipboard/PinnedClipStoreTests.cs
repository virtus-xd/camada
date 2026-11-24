using CamAda.Core.Clipboard;

namespace CamAda.Core.Tests.Clipboard;

public class PinnedClipStoreTests : IDisposable
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 24, 15, 0, 0, TimeSpan.FromHours(3));
    private readonly string _dir = Directory.CreateTempSubdirectory("camada-clip-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string Folder => Path.Combine(_dir, "clipboard");

    [Fact]
    public void Text_and_formats_round_trip()
    {
        var item = new ClipItem("t1", ClipKind.Code, ClipPayload.FromText("var x = 1;", "<pre>x</pre>", @"{\rtf1 x}"), T0, true);
        new PinnedClipStore(Folder).Save([item]);

        var loaded = Assert.Single(new PinnedClipStore(Folder).Load());

        Assert.Equal("t1", loaded.Id);
        Assert.Equal(ClipKind.Code, loaded.Kind);
        Assert.Equal("var x = 1;", loaded.Payload.Text);
        Assert.Equal("<pre>x</pre>", loaded.Payload.Html);
        Assert.Equal(@"{\rtf1 x}", loaded.Payload.Rtf);
        Assert.Equal(T0, loaded.CopiedAt);
        Assert.True(loaded.Pinned);
    }

    [Fact]
    public void Image_bytes_round_trip()
    {
        var png = ValidPng();
        var item = new ClipItem("g1", ClipKind.Image, ClipPayload.FromImage(png, 100, 60, "hash"), T0, true);
        new PinnedClipStore(Folder).Save([item]);

        var loaded = Assert.Single(new PinnedClipStore(Folder).Load());

        Assert.Equal(png, loaded.Payload.Png);
        Assert.Equal(100, loaded.Payload.Width);
        Assert.Equal(60, loaded.Payload.Height);
        Assert.Equal("hash", loaded.Payload.PixelHash);
        Assert.Equal(ClipKind.Image, loaded.Kind);
    }

    [Fact]
    public void Saving_fewer_items_deletes_their_images()
    {
        var store = new PinnedClipStore(Folder);
        var image = new ClipItem("g1", ClipKind.Image, ClipPayload.FromImage([1, 2], 1, 1, "h"), T0, true);
        var text = new ClipItem("t1", ClipKind.Text, ClipPayload.FromText("kalsın"), T0, true);
        store.Save([image, text]);
        Assert.True(File.Exists(Path.Combine(Folder, "g1.png")));

        store.Save([text]);

        Assert.False(File.Exists(Path.Combine(Folder, "g1.png")));
        Assert.Equal("t1", Assert.Single(store.Load()).Id);
    }

    [Fact]
    public void Corrupt_file_loads_empty()
    {
        Directory.CreateDirectory(Folder);
        File.WriteAllText(Path.Combine(Folder, "pinned.json"), "{ bozuk");

        Assert.Empty(new PinnedClipStore(Folder).Load());
    }

    [Fact]
    public void Missing_folder_loads_empty() => Assert.Empty(new PinnedClipStore(Folder).Load());

    // Son inceleme, küçük 3 (yükseltildi): bozuk PNG her panel açılışında kabuğu çökertiyordu.
    [Fact]
    public void Corrupt_image_is_skipped()
    {
        var store = new PinnedClipStore(Folder);
        var image = new ClipItem("g1", ClipKind.Image, ClipPayload.FromImage(ValidPng(), 2, 2, "h"), T0, true);
        var text = new ClipItem("t1", ClipKind.Text, ClipPayload.FromText("kalsın"), T0, true);
        store.Save([image, text]);
        File.WriteAllBytes(Path.Combine(Folder, "g1.png"), [1, 2, 3, 4]);

        Assert.Equal("t1", Assert.Single(new PinnedClipStore(Folder).Load()).Id);
    }

    // Son inceleme, küçük 2 (yükseltildi): okunamayan kayıt sonraki kaydetmede tüm sabitlileri siliyordu.
    [Fact]
    public void Failed_load_blocks_saving_so_pins_are_not_lost()
    {
        var first = new PinnedClipStore(Folder);
        first.Save([new ClipItem("g1", ClipKind.Image, ClipPayload.FromImage(ValidPng(), 2, 2, "h"), T0, true)]);
        var json = File.ReadAllText(Path.Combine(Folder, "pinned.json"));

        var store = new PinnedClipStore(Folder);
        using (new FileStream(Path.Combine(Folder, "g1.png"), FileMode.Open, FileAccess.Read, FileShare.None))
        {
            Assert.Empty(store.Load());
        }

        Assert.False(store.CanSave);
        store.Save([new ClipItem("t1", ClipKind.Text, ClipPayload.FromText("yeni"), T0, true)]);
        Assert.True(File.Exists(Path.Combine(Folder, "g1.png")));
        Assert.Equal(json, File.ReadAllText(Path.Combine(Folder, "pinned.json")));
    }

    /// <summary>Üst bilgisi geçerli küçük bir PNG (yalnız imza + IHDR yeter; içerik çözülmez).</summary>
    private static byte[] ValidPng()
    {
        var bytes = new byte[33];
        byte[] signature = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
        signature.CopyTo(bytes, 0);
        "IHDR"u8.ToArray().CopyTo(bytes, 12);
        bytes[19] = 2;
        bytes[23] = 2;
        return bytes;
    }
}
