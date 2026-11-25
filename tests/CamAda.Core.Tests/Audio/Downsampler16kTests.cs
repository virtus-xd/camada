using CamAda.Core.Audio;

namespace CamAda.Core.Tests.Audio;

public class Downsampler16kTests
{
    private static float[] Interleaved(int frames, params float[] channels)
    {
        var data = new float[frames * channels.Length];
        for (var f = 0; f < frames; f++)
        {
            for (var c = 0; c < channels.Length; c++)
            {
                data[f * channels.Length + c] = channels[c];
            }
        }

        return data;
    }

    [Fact]
    public void Stereo_48k_to_mono_16k()
    {
        var output = new Downsampler16k(48000, 2).Process(Interleaved(4800, 0.5f, 0.5f));
        Assert.Equal(1600, output.Length);
        Assert.All(output, s => Assert.InRange(s, (short)16382, (short)16384));
    }

    [Fact]
    public void Channels_are_averaged()
    {
        var output = new Downsampler16k(48000, 2).Process(Interleaved(480, 1.0f, 0f));
        Assert.All(output, s => Assert.InRange(s, (short)16382, (short)16384));
    }

    [Fact]
    public void Chunks_keep_count()
    {
        var down = new Downsampler16k(48000, 1);
        var total = 0;
        for (var i = 0; i < 3; i++)
        {
            total += down.Process(Interleaved(1000, 0.1f)).Length;
        }

        Assert.InRange(total, 999, 1001);
    }

    [Fact]
    public void Rate_44100() => Assert.InRange(new Downsampler16k(44100, 1).Process(Interleaved(4410, 0.2f)).Length, 1599, 1601);

    [Fact]
    public void Rms_and_bytes()
    {
        Assert.Equal(0.5, Pcm.Rms(Enumerable.Repeat((short)16384, 100).ToArray()), 3);
        Assert.Equal(0, Pcm.Rms([]));
        Assert.Equal(new byte[] { 0x01, 0x02, 0xFF, 0xFF }, Pcm.ToBytes([0x0201, -1]));
    }
}
