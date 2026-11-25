using CamAda.Core.Clipboard;

namespace CamAda.Core.Tests.Clipboard;

public class ClipboardHistoryTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 24, 15, 0, 0, TimeSpan.FromHours(3));

    private static ClipPayload Text(string text, string? html = null) => ClipPayload.FromText(text, html);

    [Fact]
    public void Contains_finds_same_content_in_pinned_and_recent()
    {
        var history = new ClipboardHistory([new ClipItem("p", ClipKind.Text, Text("sabit"), T0, Pinned: true)]);
        history.Add(Text("yakın"), T0);

        Assert.True(history.Contains(Text("sabit")));
        Assert.True(history.Contains(Text("yakın")));
        Assert.False(history.Contains(Text("yok")));
    }

    [Fact]
    public void Adds_newest_first()
    {
        var history = new ClipboardHistory([]);
        history.Add(Text("bir"), T0);
        history.Add(Text("iki"), T0.AddMinutes(1));

        Assert.Equal(["iki", "bir"], history.Recent.Select(i => i.Payload.Text));
        Assert.Equal(ClipKind.Text, history.Recent[0].Kind);
        Assert.False(history.Recent[0].Pinned);
    }

    [Fact]
    public void Same_text_moves_to_top_without_new_row()
    {
        var history = new ClipboardHistory([]);
        var first = history.Add(Text("aynı", html: "<b>eski</b>"), T0)!;
        history.Add(Text("başka"), T0.AddMinutes(1));

        var again = history.Add(Text("aynı", html: "<i>yeni</i>"), T0.AddMinutes(2))!;

        Assert.Equal(2, history.Recent.Count);
        Assert.Equal(first.Id, again.Id);
        Assert.Same(again, history.Recent[0]);
        Assert.Equal(T0.AddMinutes(2), again.CopiedAt);
        Assert.Equal("<i>yeni</i>", again.Payload.Html);
    }

    [Fact]
    public void Same_image_pixels_are_one_row()
    {
        var history = new ClipboardHistory([]);
        history.Add(ClipPayload.FromImage([1, 2, 3], 100, 60, "hash-a"), T0);
        history.Add(ClipPayload.FromImage([9, 9, 9, 9], 100, 60, "hash-a"), T0.AddMinutes(1));

        var item = Assert.Single(history.Recent);
        Assert.Equal(ClipKind.Image, item.Kind);
        Assert.Equal([9, 9, 9, 9], item.Payload.Png);
    }

    [Fact]
    public void Keeps_30_recent_and_drops_oldest()
    {
        var history = new ClipboardHistory([]);
        history.Pin(history.Add(Text("sabit-1"), T0)!.Id);
        history.Pin(history.Add(Text("sabit-2"), T0)!.Id);

        for (var i = 1; i <= 31; i++)
        {
            history.Add(Text($"öğe {i}"), T0.AddSeconds(i));
        }

        Assert.Equal(ClipboardHistory.MaxRecent, history.Recent.Count);
        Assert.Equal("öğe 31", history.Recent[0].Payload.Text);
        Assert.Equal("öğe 2", history.Recent[^1].Payload.Text);
        Assert.DoesNotContain(history.Recent, i => i.Payload.Text == "öğe 1");
        Assert.Equal(2, history.Pinned.Count);
    }

    [Fact]
    public void Rejects_blank_and_oversized()
    {
        var history = new ClipboardHistory([]);
        Assert.Null(history.Add(Text(" \n\t"), T0));

        var max = new byte[ClipboardHistory.MaxItemBytes];
        Assert.NotNull(history.Add(ClipPayload.FromImage(max, 1, 1, "büyük"), T0));
        var over = new byte[ClipboardHistory.MaxItemBytes + 1];
        Assert.Null(history.Add(ClipPayload.FromImage(over, 1, 1, "çok-büyük"), T0));
        Assert.Single(history.Recent);
    }

    [Fact]
    public void Pin_moves_to_pinned_and_unpin_returns_to_top_of_recent()
    {
        var history = new ClipboardHistory([]);
        var item = history.Add(Text("sabitlenecek"), T0)!;
        history.Add(Text("sonraki"), T0.AddMinutes(1));
        var version = history.PinnedVersion;

        history.Pin(item.Id);

        var pinned = Assert.Single(history.Pinned);
        Assert.True(pinned.Pinned);
        Assert.Equal(item.Id, pinned.Id);
        Assert.DoesNotContain(history.Recent, i => i.Id == item.Id);
        Assert.True(history.PinnedVersion > version);
        version = history.PinnedVersion;

        history.Unpin(item.Id);

        Assert.Empty(history.Pinned);
        Assert.Equal(item.Id, history.Recent[0].Id);
        Assert.False(history.Recent[0].Pinned);
        Assert.True(history.PinnedVersion > version);
    }

    [Fact]
    public void Copying_a_pinned_text_again_keeps_it_pinned()
    {
        var history = new ClipboardHistory([]);
        var item = history.Add(Text("adres"), T0)!;
        history.Pin(item.Id);
        history.Add(Text("başka"), T0.AddMinutes(1));
        var version = history.PinnedVersion;

        history.Add(Text("adres"), T0.AddMinutes(5));

        Assert.Equal(["başka"], history.Recent.Select(i => i.Payload.Text));
        var pinned = Assert.Single(history.Pinned);
        Assert.Equal(T0.AddMinutes(5), pinned.CopiedAt);
        Assert.True(pinned.Pinned);
        Assert.True(history.PinnedVersion > version);
    }

    [Fact]
    public void ClearUnpinned_keeps_pinned()
    {
        var history = new ClipboardHistory([]);
        history.Pin(history.Add(Text("kalsın"), T0)!.Id);
        history.Add(Text("gitsin"), T0);
        var version = history.PinnedVersion;

        history.ClearUnpinned();

        Assert.Empty(history.Recent);
        Assert.Single(history.Pinned);
        Assert.Equal(version, history.PinnedVersion);
    }

    [Fact]
    public void Remove_deletes_from_either_list()
    {
        var history = new ClipboardHistory([]);
        var pinned = history.Add(Text("sabit"), T0)!;
        history.Pin(pinned.Id);
        var recent = history.Add(Text("son"), T0)!;

        history.Remove(recent.Id);
        history.Remove(pinned.Id);

        Assert.Empty(history.Recent);
        Assert.Empty(history.Pinned);
        Assert.Null(history.Find(pinned.Id));
    }

    [Fact]
    public void Touch_moves_recent_item_to_top()
    {
        var history = new ClipboardHistory([]);
        var old = history.Add(Text("eski"), T0)!;
        history.Add(Text("yeni"), T0.AddMinutes(1));

        history.Touch(old.Id, T0.AddMinutes(2));

        Assert.Equal(old.Id, history.Recent[0].Id);
        Assert.Equal(T0.AddMinutes(2), history.Recent[0].CopiedAt);
        Assert.Equal(2, history.Recent.Count);
    }

    [Fact]
    public void Loaded_pinned_items_start_in_pinned()
    {
        var saved = new ClipItem("abc", ClipKind.Text, Text("kayıtlı"), T0, Pinned: true);
        var history = new ClipboardHistory([saved]);

        Assert.Same(saved, Assert.Single(history.Pinned));
        Assert.Same(saved, history.Find("abc"));
    }
}
