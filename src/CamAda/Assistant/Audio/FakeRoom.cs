#if DEBUG
using System.Diagnostics;
using NAudio.Wave;

namespace CamAda.Assistant.Audio;

/// <summary>
/// Yalnız Debug: <c>CAMADA_ADA_FAKE_ROOM</c>'daki 16 kHz mono 16 bit WAV, ilk sahte mikrofon açıldığında çalmaya başlayan
/// bir oda gibi davranır; dosya bitince oda sessizdir. Gerçek mikrofon gibi her mikrofon yalnız açık olduğu sürede
/// duyduğunu verir.
/// </summary>
internal static class FakeRoom
{
    public const int Rate = 16000;
    private static readonly object Lock = new();
    private static short[]? _audio;
    private static long _startedAt;

    public static bool Active => Environment.GetEnvironmentVariable("CAMADA_ADA_FAKE_ROOM") is { Length: > 0 };

    /// <summary>Odanın saati (örnek); ilk çağrıda dosya yüklenir ve saat başlar.</summary>
    public static long Now()
    {
        lock (Lock)
        {
            if (_audio is null)
            {
                _audio = Load(Environment.GetEnvironmentVariable("CAMADA_ADA_FAKE_ROOM")!);
                _startedAt = Stopwatch.GetTimestamp();
                Assistant.AdaProbes.Log($"sahte oda: {_audio.Length / (double)Rate:F1} sn ses çalmaya başladı");
            }

            return (long)(Stopwatch.GetElapsedTime(_startedAt).TotalSeconds * Rate);
        }
    }

    public static short[] Read(long from, int count)
    {
        var audio = _audio ?? [];
        var chunk = new short[count];
        for (var i = 0; i < count; i++)
        {
            var at = from + i;
            chunk[i] = at >= 0 && at < audio.Length ? audio[at] : (short)0;
        }

        return chunk;
    }

    private static short[] Load(string path)
    {
        using var reader = new WaveFileReader(path);
        if (reader.WaveFormat is not { SampleRate: Rate, Channels: 1, BitsPerSample: 16 })
        {
            throw new InvalidOperationException("Sahte oda sesi 16 kHz mono 16 bit olmalı: " + reader.WaveFormat);
        }

        var bytes = new byte[reader.Length];
        var read = reader.Read(bytes, 0, bytes.Length);
        var samples = new short[read / 2];
        Buffer.BlockCopy(bytes, 0, samples, 0, read);
        return samples;
    }
}

/// <summary>Yalnız Debug: odayı "duyan" mikrofon; yankı engelleyici sürümü açılışta ~0,3 sn gecikir (DSP gibi).</summary>
internal sealed class FakeMic(bool echoCancelled) : IAdaMic
{
    private const int ChunkSamples = 1600;
    private Thread? _thread;
    private volatile bool _stop;

    public event Action<short[]>? Samples;

    public event Action<string>? Failed;

    public bool EchoCancelled => echoCancelled;

    public void Start()
    {
        if (!echoCancelled && Environment.GetEnvironmentVariable("CAMADA_ADA_MIC_DENIED") == "1")
        {
            throw new UnauthorizedAccessException("deneme: mikrofon izni kapalı sayıldı");
        }

        _thread = new Thread(Run) { IsBackground = true, Name = "Ada fake microphone" };
        _thread.Start();
    }

    public void Dispose()
    {
        _stop = true;
        _thread?.Join(1000);
    }

    private void Run()
    {
        Thread.Sleep(echoCancelled ? 300 : 30);
        if (echoCancelled && Environment.GetEnvironmentVariable("CAMADA_ADA_NO_AEC") == "1")
        {
            Thread.Sleep(700); // DSP'nin ilk veri bekleme süresi
            if (!_stop)
            {
                Failed?.Invoke("deneme: yankı engelleyici kapalı sayıldı"); // i18n-ignore: geliştirici denemesi
            }

            return;
        }

        var position = FakeRoom.Now();
        while (!_stop)
        {
            Thread.Sleep(20);
            var now = FakeRoom.Now();
            while (!_stop && now - position >= ChunkSamples)
            {
                Samples?.Invoke(FakeRoom.Read(position, ChunkSamples));
                position += ChunkSamples;
            }
        }
    }
}
#endif
