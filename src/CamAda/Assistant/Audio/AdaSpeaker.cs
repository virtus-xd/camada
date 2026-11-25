using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace CamAda.Assistant.Audio;

public enum AdaCue
{
    /// <summary>Uyanınca: iki yükselen kısa ton.</summary>
    Wake,

    /// <summary>Hata: tek, alçak kısa ton.</summary>
    Error,
}

/// <summary>
/// Ada'nın sesi: Live'dan gelen 24 kHz mono 16 bit PCM, cihazın biçimine çevrilip çalınır. Söz kesilince
/// <see cref="Clear"/> kalan sesi atar.
/// </summary>
internal sealed class AdaSpeaker : IDisposable
{
    private const double CueAmplitude = 0.126; // −18 dBFS
    private readonly BufferedWaveProvider _buffer;
    private readonly WasapiOut _player;

    public AdaSpeaker(MMDevice device)
    {
        DeviceId = device.ID;
        _buffer = new BufferedWaveProvider(new WaveFormat(24000, 16, 1))
        {
            BufferDuration = TimeSpan.FromMinutes(3),
            DiscardOnBufferOverflow = true,
        };

        var mix = device.AudioClient.MixFormat;
        ISampleProvider chain = _buffer.ToSampleProvider();
        if (mix.SampleRate != 24000)
        {
            chain = new WdlResamplingSampleProvider(chain, mix.SampleRate);
        }

        if (mix.Channels == 2)
        {
            chain = new MonoToStereoSampleProvider(chain);
        }
        else if (mix.Channels > 2)
        {
            var multiplex = new MultiplexingSampleProvider([chain], mix.Channels);
            for (var c = 0; c < mix.Channels; c++)
            {
                multiplex.ConnectInputToOutput(0, c);
            }

            chain = multiplex;
        }

        _player = new WasapiOut(device, AudioClientShareMode.Shared, true, 60);
        _player.Init(new SampleToWaveProvider(chain));
        _player.Play();
    }

    /// <summary>
    /// Çaldığı cihazın kimliği. Cihaz nesnesinin kendisi tutulmaz: NAudio'nun MMDevice'ı oluşturulduğu COM dairesinden
    /// çıkamıyor (E_NOINTERFACE); Ada hoparlörü arka planda (MTA) kurar ve atar.
    /// </summary>
    public string DeviceId { get; }

    /// <summary>Çalınmayı bekleyen ses var mı.</summary>
    public bool IsPlaying => _buffer.BufferedBytes > 0;

    public void Add(byte[] pcm24k) => _buffer.AddSamples(pcm24k, 0, pcm24k.Length);

    public void Clear() => _buffer.ClearBuffer();

    public void PlayCue(AdaCue cue)
    {
        var tone = cue == AdaCue.Wake
            ? Tone(660, 80).Concat(Tone(880, 80)).ToArray()
            : Tone(440, 200);
        Add(tone);
    }

    public void Dispose()
    {
        _player.Stop();
        _player.Dispose();
    }

    /// <summary>24 kHz'te yumuşak girişli ve çıkışlı (10 ms) sinüs tonu.</summary>
    private static byte[] Tone(double frequency, int milliseconds)
    {
        const int rate = 24000;
        var count = rate * milliseconds / 1000;
        var fade = rate / 100;
        var bytes = new byte[count * 2];
        for (var i = 0; i < count; i++)
        {
            var envelope = Math.Min(1.0, Math.Min(i, count - 1 - i) / (double)fade);
            var value = (short)(Math.Sin(2 * Math.PI * frequency * i / rate) * CueAmplitude * envelope * short.MaxValue);
            BitConverter.TryWriteBytes(bytes.AsSpan(i * 2), value);
        }

        return bytes;
    }
}
