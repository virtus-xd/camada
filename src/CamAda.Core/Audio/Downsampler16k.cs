namespace CamAda.Core.Audio;

/// <summary>
/// Mikrofonun biçimini (ör. 48 kHz stereo float) 16 kHz mono 16 bit PCM'e çevirir: kanallar ortalanır, her çıkış örneği
/// oran penceresindeki girişlerin ortalamasıdır (kaba alçak geçiren süzgeç). Durum parçalar arasında korunur.
/// </summary>
public sealed class Downsampler16k(int inputRate, int channels)
{
    private readonly double _ratio = inputRate / 16000.0;
    private readonly List<float> _pending = new();
    private double _position;

    public short[] Process(ReadOnlySpan<float> interleaved)
    {
        var frames = interleaved.Length / channels;
        for (var f = 0; f < frames; f++)
        {
            float sum = 0;
            for (var c = 0; c < channels; c++)
            {
                sum += interleaved[f * channels + c];
            }

            _pending.Add(sum / channels);
        }

        var output = new List<short>();
        while (_position + _ratio <= _pending.Count)
        {
            var start = (int)_position;
            var end = Math.Max(start + 1, (int)(_position + _ratio));
            float acc = 0;
            for (var i = start; i < end; i++)
            {
                acc += _pending[i];
            }

            output.Add((short)Math.Clamp(acc / (end - start) * 32767f, short.MinValue, short.MaxValue));
            _position += _ratio;
        }

        var consumed = (int)_position;
        _pending.RemoveRange(0, consumed);
        _position -= consumed;
        return output.ToArray();
    }
}

public static class Pcm
{
    /// <summary>16 bit küçük-sonlu bayt dizisi.</summary>
    public static byte[] ToBytes(short[] samples)
    {
        var bytes = new byte[samples.Length * 2];
        Buffer.BlockCopy(samples, 0, bytes, 0, bytes.Length);
        return bytes;
    }

    /// <summary>Kök ortalama kare, 0–1.</summary>
    public static double Rms(ReadOnlySpan<short> samples)
    {
        if (samples.Length == 0)
        {
            return 0;
        }

        double energy = 0;
        foreach (var s in samples)
        {
            var v = s / 32768.0;
            energy += v * v;
        }

        return Math.Sqrt(energy / samples.Length);
    }
}
