using System.IO;
using SherpaOnnx;

namespace CamAda.Assistant.Audio;

/// <summary>
/// Konuşmacı gömmesi (misafir kipi): 16 kHz mono sesten, sesin kime ait olduğunu özetleyen sayı dizisi. Tek çekirdek,
/// bilgisayarda. Aynı anda tek hesap (kilit); her çağrı kendi akışını açar.
/// </summary>
internal sealed class SpeakerEmbedder : IDisposable
{
    private readonly SpeakerEmbeddingExtractor _extractor;
    private readonly object _lock = new();

    public SpeakerEmbedder(string modelPath)
    {
        if (!File.Exists(modelPath))
        {
            throw new FileNotFoundException("Konuşmacı modeli eksik", modelPath);
        }

        var config = new SpeakerEmbeddingExtractorConfig
        {
            Model = modelPath,
            NumThreads = 1,
            Provider = "cpu",
            Debug = 0,
        };
        _extractor = new SpeakerEmbeddingExtractor(config);
        Dim = _extractor.Dim;
    }

    public int Dim { get; }

    /// <summary>Gömme; ses model için çok kısaysa sıfır dizi (çağıran en az 0,5 sn vermeli).</summary>
    public float[] Compute(ReadOnlySpan<short> audio16k)
    {
        var floats = new float[audio16k.Length];
        for (var i = 0; i < floats.Length; i++)
        {
            floats[i] = audio16k[i] / 32768f;
        }

        lock (_lock)
        {
            using var stream = _extractor.CreateStream();
            stream.AcceptWaveform(16000, floats);
            stream.InputFinished();
            return _extractor.IsReady(stream) ? _extractor.Compute(stream) : new float[Dim];
        }
    }

    public void Dispose() => _extractor.Dispose();
}
