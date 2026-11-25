using CamAda.Core.Audio;

namespace CamAda.Core.Tests.Audio;

public class PreRollBufferTests
{
    [Fact]
    public void Keeps_last_samples_in_order_and_drains_once()
    {
        var buffer = new PreRollBuffer(32000);
        var data = Enumerable.Range(0, 40000).Select(i => (short)(i % 30000)).ToArray();
        buffer.Write(data.AsSpan(0, 25000));
        buffer.Write(data.AsSpan(25000));

        Assert.Equal(data[8000..], buffer.Drain());
        Assert.Empty(buffer.Drain());
    }

    [Fact]
    public void Drain_from_absolute_position_drops_older_samples()
    {
        var buffer = new PreRollBuffer(10);
        buffer.Write(new short[] { 1, 2, 3, 4, 5, 6 }); // mutlak 0..5
        Assert.Equal(6, buffer.Total);

        Assert.Equal(new short[] { 5, 6 }, buffer.DrainFrom(4)); // uyandırma sözü 4. örnekte başladı: öncesi gitmez
        Assert.Empty(buffer.Drain());
        Assert.Equal(6, buffer.Total);

        buffer.Write(Enumerable.Range(7, 12).Select(i => (short)i).ToArray()); // mutlak 6..17, arabellekte 8..17
        Assert.Equal(Enumerable.Range(9, 10).Select(i => (short)i), buffer.DrainFrom(2)); // eskisi zaten düştü: hepsi
        buffer.Write(new short[] { 1, 2 });
        Assert.Empty(buffer.DrainFrom(100)); // henüz yazılmamış konum
    }

    [Fact]
    public void Short_write_drains_as_is()
    {
        var buffer = new PreRollBuffer(10);
        buffer.Write(new short[] { 1, 2, 3 });
        Assert.Equal(new short[] { 1, 2, 3 }, buffer.Drain());
    }
}
