using System.IO;
using CamAda.Core.Assistant;
using CamAda.Core.Settings;
using SherpaOnnx;

namespace CamAda.Assistant.Audio;

/// <summary>
/// Misafir kipi süzgecinin yerel çözümleyicisi: Silero ile konuşmayı algılar, uyandırma modeliyle "Ada"yı yakalar, konuşmacı
/// modeliyle sesi ses iziyle karşılaştırır. Hepsi bilgisayarda, tek çekirdek. Oturumlar arasında paylaşılır; aynı anda iki
/// süzgeç (kapanan ve açılan oturum) dokunabileceği için her çağrı kilitlidir.
/// </summary>
internal sealed class SherpaVoiceAnalyzer : IVoiceAnalyzer, IDisposable
{
    private readonly object _lock = new();
    private readonly SpeakerEmbedder _embedder;
    private readonly VoiceActivityDetector _vad;
    private readonly KeywordSpotter _spotter;
    private OnlineStream _name;

    private SherpaVoiceAnalyzer(string speakerDir, string wakeModelDir)
    {
        _embedder = new SpeakerEmbedder(Path.Combine(speakerDir, VoiceModels.SpeakerModel));
        var config = new VadModelConfig();
        config.SileroVad.Model = Path.Combine(speakerDir, VoiceModels.Vad);
        config.SileroVad.Threshold = 0.5f;
        config.SileroVad.MinSilenceDuration = 0.25f;
        config.SileroVad.MinSpeechDuration = 0.25f;
        config.SileroVad.WindowSize = 512;
        config.SileroVad.MaxSpeechDuration = 20f;
        config.SampleRate = 16000;
        config.NumThreads = 1;
        config.Provider = "cpu";
        _vad = new VoiceActivityDetector(config, 30);
        _spotter = WakeWordListener.CreateSpotter(wakeModelDir, WakeWord.NameKeywordLine(WakeWord.NameThreshold, WakeWord.NameBoost),
            "ada-ad.txt", WakeWord.NameThreshold, WakeWord.NameBoost);
        _name = _spotter.CreateStream();
    }

    /// <summary>Ses izi; null ise her ses kullanıcı sayılır (yalnız elle açılan kipin "Ada" kuralı çalışır).</summary>
    public Voiceprint? Voiceprint { get; set; }

    public double Threshold { get; set; } = AssistantSettings.DefaultSpeakerThreshold;

    /// <summary>
    /// Modeller yerinde değilse null (eksik dosyayla sherpa-onnx süreci düşürebilir: yerel nesne kurulmadan önce denetlenir);
    /// kurulamazsa günlüğe yazar ve null.
    /// </summary>
    public static SherpaVoiceAnalyzer? TryCreate(string speakerDir, string wakeModelDir, Action<Exception> log)
    {
        if (!VoiceModels.Present(speakerDir) || WakeWordListener.MissingModelFile(wakeModelDir) is not null)
        {
            return null;
        }

        try
        {
            return new SherpaVoiceAnalyzer(speakerDir, wakeModelDir);
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            log(ex);
            return null;
        }
    }

    /// <summary>Yeni oturum: konuşma algılayıcının ve "Ada" akışının önceki oturumdan kalanı silinir.</summary>
    public void Reset()
    {
        lock (_lock)
        {
            _vad.Reset();
            _name.Dispose();
            _name = _spotter.CreateStream();
        }
    }

    public bool IsSpeech(short[] chunk)
    {
        lock (_lock)
        {
            _vad.AcceptWaveform(ToFloats(chunk));
            var speech = _vad.IsSpeechDetected();
            while (!_vad.IsEmpty())
            {
                _vad.Pop(); // biten parçalar kullanılmaz; kuyruk büyümesin
            }

            return speech;
        }
    }

    public bool HeardName(short[] chunk)
    {
        lock (_lock)
        {
            var heard = false;
            _name.AcceptWaveform(16000, ToFloats(chunk));
            while (_spotter.IsReady(_name))
            {
                _spotter.Decode(_name);
                if (!string.IsNullOrEmpty(_spotter.GetResult(_name).Keyword))
                {
                    _spotter.Reset(_name);
                    heard = true;
                }
            }

            return heard;
        }
    }

    public SpeakerVerdict Verify(short[] segment)
    {
        lock (_lock)
        {
            if (Voiceprint is not { } print)
            {
                return SpeakerVerdict.User;
            }

            var embedding = _embedder.Compute(segment);
            if (embedding.All(v => v == 0))
            {
                return SpeakerVerdict.User; // model yargılayamadı: kullanıcıyı engellememek için
            }

            return print.Score(embedding) >= Threshold ? SpeakerVerdict.User : SpeakerVerdict.Other;
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _name.Dispose();
            _spotter.Dispose();
            _vad.Dispose();
            _embedder.Dispose();
        }
    }

    private static float[] ToFloats(short[] samples)
    {
        var floats = new float[samples.Length];
        for (var i = 0; i < floats.Length; i++)
        {
            floats[i] = samples[i] / 32768f;
        }

        return floats;
    }
}
