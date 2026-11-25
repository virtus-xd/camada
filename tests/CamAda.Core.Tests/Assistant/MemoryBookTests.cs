using CamAda.Core.Assistant;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Assistant;

public class MemoryBookTests
{
    private readonly FakeTimeProvider _time = new();

    private DateTimeOffset Tick()
    {
        _time.Advance(TimeSpan.FromSeconds(1));
        return _time.GetUtcNow();
    }

    [Fact]
    public void Remember_saves_and_updates_same_topic()
    {
        var book = new MemoryBook();
        Assert.Equal(new MemoryChange.Saved(null), book.Remember("şehir", "Ankara", Tick()));
        Assert.IsType<MemoryChange.Updated>(book.Remember("Şehir", "İzmir", Tick()));
        var item = Assert.Single(book.Items);
        Assert.Equal("şehir", item.Topic);
        Assert.Equal("İzmir", item.Fact);
    }

    [Fact]
    public void Rejects_sensitive_too_long_and_empty()
    {
        var book = new MemoryBook();
        Assert.Equal(new MemoryChange.Rejected("hassas bilgi"), book.Remember("kart", "4111 1111 1111 1111", Tick()));
        Assert.Equal(new MemoryChange.Rejected("çok uzun"), book.Remember("not", new string('a', 201), Tick()));
        Assert.Equal(new MemoryChange.Rejected("eksik bilgi"), book.Remember(" ", "x", Tick()));
        Assert.Empty(book.Items);
    }

    [Fact]
    public void Fifty_first_evicts_oldest_updated()
    {
        var book = new MemoryBook();
        for (var i = 0; i < MemoryBook.MaxItems; i++)
        {
            book.Remember($"k{i}", $"bilgi {i}", Tick());
        }

        book.Remember("k0", "güncel", Tick());
        var change = Assert.IsType<MemoryChange.Saved>(book.Remember("yeni", "x", Tick()));
        Assert.Equal("k1", change.Evicted?.Topic);
        Assert.Equal(MemoryBook.MaxItems, book.Items.Count);
    }

    [Fact]
    public void Forget_and_clear()
    {
        var book = new MemoryBook();
        book.Remember("şehir", "Ankara", Tick());
        book.Remember("kulaklık", "Buds2", Tick());
        Assert.True(book.Forget("ŞEHİR"));
        Assert.False(book.Forget("şehir"));
        book.Clear();
        Assert.Empty(book.Items);
    }

    [Fact]
    public void Json_round_trip()
    {
        var book = new MemoryBook();
        book.Remember("şehir", "Ankara", Tick());
        book.Remember("kulaklık", "Buds2", Tick());
        Assert.Equal(book.Items, MemoryBook.FromJson(book.ToJson()).Items);
        Assert.Throws<FormatException>(() => MemoryBook.FromJson("{"));
    }
}
