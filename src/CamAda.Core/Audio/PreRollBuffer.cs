namespace CamAda.Core.Audio;

/// <summary>
/// Son N örneği tutan halka arabellek: "Hey Ada"dan sonra söylenenler, Live bağlantısı kurulana kadar kaybolmasın diye.
/// Örnekler mutlak konumla da sayılır (<see cref="Total"/>): uyandırma sözünün başından öncesi ayıklanabilsin.
/// </summary>
public sealed class PreRollBuffer(int capacitySamples)
{
    private readonly short[] _ring = new short[capacitySamples];
    private int _start;
    private int _count;

    /// <summary>Şimdiye kadar yazılan örnek sayısı (boşaltma sıfırlamaz).</summary>
    public long Total { get; private set; }

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

        Total += samples.Length;
    }

    /// <summary>Tüm içeriği eskiden yeniye verir ve arabelleği boşaltır.</summary>
    public short[] Drain() => DrainFrom(0);

    /// <summary>Mutlak <paramref name="position"/>'dan (dahil) sonrasını verir; öncesi atılır; arabellek boşalır.</summary>
    public short[] DrainFrom(long position)
    {
        var oldest = Total - _count;
        var skip = (int)Math.Clamp(position - oldest, 0, _count);
        var result = new short[_count - skip];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = _ring[(_start + skip + i) % _ring.Length];
        }

        _start = 0;
        _count = 0;
        return result;
    }
}
