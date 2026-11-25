namespace CamAda.Core.Collections;

/// <summary>
/// En fazla <c>capacity</c> öğe tutan önbellek: dolunca en uzun süredir kullanılmayan düşer (ör. küçük resimler; sınırsız
/// büyümesin). İş parçacığı güvenli değildir.
/// </summary>
public sealed class LruCache<TKey, TValue> where TKey : notnull
{
    private readonly int _capacity;
    private readonly Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>> _items;
    private readonly LinkedList<(TKey Key, TValue Value)> _order = new(); // baş: en son kullanılan

    public LruCache(int capacity, IEqualityComparer<TKey>? comparer = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _capacity = capacity;
        _items = new Dictionary<TKey, LinkedListNode<(TKey Key, TValue Value)>>(comparer);
    }

    public int Count => _items.Count;

    public bool TryGet(TKey key, out TValue value)
    {
        if (!_items.TryGetValue(key, out var node))
        {
            value = default!;
            return false;
        }

        _order.Remove(node);
        _order.AddFirst(node);
        value = node.Value.Value;
        return true;
    }

    public void Set(TKey key, TValue value)
    {
        if (_items.TryGetValue(key, out var existing))
        {
            _order.Remove(existing);
        }
        else if (_items.Count >= _capacity && _order.Last is { } oldest)
        {
            _order.RemoveLast();
            _items.Remove(oldest.Value.Key);
        }

        _items[key] = _order.AddFirst((key, value));
    }
}
