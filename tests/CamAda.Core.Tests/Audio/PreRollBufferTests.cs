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
    public void Short_write_drains_as_is()
    {
        var buffer = new PreRollBuffer(10);
        buffer.Write(new short[] { 1, 2, 3 });
        Assert.Equal(new short[] { 1, 2, 3 }, buffer.Drain());
    }
}
