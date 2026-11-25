using CamAda.Core.Collections;

namespace CamAda.Core.Tests.Collections;

public class LruCacheTests
{
    [Fact]
    public void Keeps_at_most_capacity_items_dropping_the_least_recently_used()
    {
        var cache = new LruCache<string, int>(capacity: 2);
        cache.Set("a", 1);
        cache.Set("b", 2);
        Assert.True(cache.TryGet("a", out _)); // a yeni kullanıldı: düşen b olmalı
        cache.Set("c", 3);

        Assert.Equal(2, cache.Count);
        Assert.True(cache.TryGet("a", out var a));
        Assert.Equal(1, a);
        Assert.False(cache.TryGet("b", out _));
        Assert.True(cache.TryGet("c", out _));
    }

    [Fact]
    public void Setting_an_existing_key_replaces_without_growing()
    {
        var cache = new LruCache<string, int>(capacity: 2);
        cache.Set("a", 1);
        cache.Set("a", 5);

        Assert.Equal(1, cache.Count);
        Assert.True(cache.TryGet("a", out var a));
        Assert.Equal(5, a);
    }

    [Fact]
    public void Key_comparer_is_honoured()
    {
        var cache = new LruCache<string, int>(capacity: 2, StringComparer.OrdinalIgnoreCase);
        cache.Set(@"C:\A.png", 1);
        Assert.True(cache.TryGet(@"c:\a.PNG", out _));
    }
}
