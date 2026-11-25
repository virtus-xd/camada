namespace CamAda.Core.Audio;

/// <summary>
/// Son N örneği tutan halka arabellek: "Hey Ada"dan sonra söylenenler, Live bağlantısı kurulana kadar kaybolmasın diye.
/// </summary>
public sealed class PreRollBuffer(int capacitySamples)
{
    private readonly short[] _ring = new short[capacitySamples];
    private int _start;
    private int _count;

    public void Write(ReadOnlySpan<short> samples)
    {
        foreach (var s in samples)
        {
            var end = (_start + _count) % _ring.Length;
            _ring[end] = s;
            if (_count < _ring.Length)
            {
                _count++;
            }
            else
            {
                _start = (_start + 1) % _ring.Length;
            }
        }
    }

    /// <summary>Tüm içeriği eskiden yeniye verir ve arabelleği boşaltır.</summary>
    public short[] Drain()
    {
        var result = new short[_count];
        for (var i = 0; i < _count; i++)
        {
            result[i] = _ring[(_start + i) % _ring.Length];
        }

        _start = 0;
        _count = 0;
        return result;
    }
}
