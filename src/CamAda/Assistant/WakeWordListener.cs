using System.IO;
using System.Text;
using CamAda.Assistant.Audio;
using CamAda.Core.Assistant;
using CamAda.Core.Audio;
using NAudio.CoreAudioApi;
using SherpaOnnx;

namespace CamAda.Assistant;

/// <summary>
/// "Hey Ada" dinleyicisi: seçili mikrofonu 16 kHz'te dinler ve sherpa-onnx anahtar sözcük algılayıcısına verir (eğitimsiz,
/// gigaspeech 3.3M int8; tek iş parçacığı, tek çekirdeğin ~%3–4'ü). Ses bilgisayarda kalır. Son 2 sn ön kayıtta tutulur:
/// "Hey Ada, sesi kıs" tek nefeste söylenince komut kaybolmasın. Oturuma yalnız algılamadan 0,8 sn öncesinden sonrası
/// gider (algılama "Ada"nın bitişinden ~0,3 sn sonra gelir; TTS ölçümü): uyandırma sözünden önceki konuşma bilgisayarda
/// kalır. Oturum mikrofonu ilk sesini verene kadar dinleyici kayda devam eder, aradaki söz de kaybolmaz.
/// </summary>
internal sealed class WakeWordListener : IDisposable
{
    private const string Encoder = "encoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx";
    private const string Decoder = "decoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx";
    private const string Joiner = "joiner-epoch-12-avg-2-chunk-16-left-64.int8.onnx";
    private const int WakeLeadSamples = 16000 * 8 / 10;

    private readonly KeywordSpotter _spotter;
    private readonly Func<MMDevice?> _mic;
    private readonly PreRollBuffer _preRoll = new(32000);
    private readonly object _lock = new();
    private IAdaMic? _capture;
    private OnlineStream? _stream;
    private long _detectedAt = -1;

    public WakeWordListener(string modelDir, double threshold, Func<MMDevice?> mic)
    {
        _mic = mic;
        // Eksik ya da silinmiş model dosyasıyla yerel kitaplık geçersiz tutamaçla sürüp süreci düşürebilir: önceden denetlenir.
        if (MissingModelFile(modelDir) is { } missing)
        {
            throw new FileNotFoundException("Uyandırma modeli eksik: " + missing, Path.Combine(modelDir, missing));
        }

        _spotter = CreateSpotter(modelDir, WakeWord.KeywordLine(threshold), "ada-uyandirma.txt", threshold, WakeWord.Boost);
    }

    /// <summary>Uyandırma modelinin eksik ilk dosyası; hepsi varsa null.</summary>
    internal static string? MissingModelFile(string modelDir) =>
        new[] { Encoder, Decoder, Joiner, "tokens.txt" }.FirstOrDefault(f => !File.Exists(Path.Combine(modelDir, f)));

    /// <summary>
    /// Uyandırma modeliyle anahtar sözcük algılayıcısı ("Hey Ada" ve misafir kipindeki "Ada"). Anahtar satırı
    /// <c>%LOCALAPPDATA%\CamAda\<paramref name="keywordsFileName"/></c> dosyasıyla verilir: KeywordsBuf C# köprüsünde ANSI'ye
    /// çevriliyor ve "▁" "?" oluyor (Plan 5 Görev 4).
    /// </summary>
    internal static KeywordSpotter CreateSpotter(string modelDir, string keywordLine, string keywordsFileName, double threshold, double boost)
    {
        var config = new KeywordSpotterConfig();
        config.FeatConfig.SampleRate = 16000;
        config.FeatConfig.FeatureDim = 80;
        config.ModelConfig.Transducer.Encoder = Path.Combine(modelDir, Encoder);
        config.ModelConfig.Transducer.Decoder = Path.Combine(modelDir, Decoder);
        config.ModelConfig.Transducer.Joiner = Path.Combine(modelDir, Joiner);
        config.ModelConfig.Tokens = Path.Combine(modelDir, "tokens.txt");
        config.ModelConfig.Provider = "cpu";
        config.ModelConfig.NumThreads = 1;
        config.ModelConfig.Debug = 0;
        var keywordsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CamAda", keywordsFileName);
        Directory.CreateDirectory(Path.GetDirectoryName(keywordsPath)!);
        File.WriteAllText(keywordsPath, keywordLine + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        config.KeywordsFile = keywordsPath;
        config.KeywordsThreshold = (float)threshold;
        config.KeywordsScore = (float)boost;
        return new KeywordSpotter(config);
    }

    /// <summary>"Hey Ada" duyuldu (ses iş parçacığında).</summary>
    public event Action? Detected;

    /// <summary>Mikrofon açılamadı ya da durdu (ses iş parçacığında).</summary>
    public event Action<string>? Failed;

    public bool IsListening
    {
        get
        {
            lock (_lock)
            {
                return _capture is not null;
            }
        }
    }

    /// <summary>
    /// Dinlemeye başlar; zaten dinliyorsa dokunmaz. Mikrofon yoksa false; açılamazsa (ör. gizlilik ayarı masaüstü
    /// uygulamalarına mikrofonu kapatmış) atar ve yarım kayıt bırakmaz.
    /// </summary>
    public bool Start()
    {
        lock (_lock)
        {
            if (_capture is not null)
            {
                return true;
            }
        }

        if (_mic() is not { } device)
        {
            return false;
        }

        var capture = MicFactory.Plain(device);
        capture.Samples += OnSamples; // akış kurulana kadar gelen parçalar atılır
        capture.Failed += OnCaptureFailed;
        try
        {
            capture.Start();
        }
        catch
        {
            Release(capture);
            throw;
        }

        lock (_lock)
        {
            if (_capture is null)
            {
                _preRoll.Drain(); // önceki dinlemeden kalan ses yeni oturuma gitmesin
                _detectedAt = -1;
                _stream = _spotter.CreateStream();
                _capture = capture;
                return true;
            }
        }

        Release(capture); // bu arada başka bir çağrı başlattı
        return true;
    }

    /// <summary>
    /// Dinlemeyi bırakır ve mikrofonu serbest bırakır (oturum mikrofonu kendisi açar). Yakalama kilit dışında atılır:
    /// NAudio atarken kayıt iş parçacığını süresiz bekler, o da <see cref="OnSamples"/>'ta bu kilidi bekliyor olabilir.
    /// </summary>
    public void Stop()
    {
        IAdaMic? capture;
        OnlineStream? stream;
        lock (_lock)
        {
            capture = _capture;
            stream = _stream;
            _capture = null;
            _stream = null;
            _preRoll.Drain();
            _detectedAt = -1;
        }

        if (capture is not null)
        {
            Release(capture);
        }

        stream?.Dispose(); // kilit bırakıldıktan sonra hiçbir OnSamples artık bu akışa dokunmaz
    }

    private void Release(IAdaMic capture)
    {
        capture.Samples -= OnSamples;
        capture.Failed -= OnCaptureFailed;
        capture.Dispose();
    }

    private void OnCaptureFailed(string message) => Failed?.Invoke(message);

    /// <summary>Son boşaltmadan bu yana duyulan (en çok 2 sn, 16 kHz mono), eskiden yeniye; arabellek boşalır.</summary>
    public short[] DrainPreRoll()
    {
        lock (_lock)
        {
            return _preRoll.Drain();
        }
    }

    /// <summary>
    /// "Hey Ada" ile açılan oturumun ilk sesi: son algılamadan 0,8 sn öncesinden bugüne; daha öncesi atılır (bilgisayarda
    /// kalır). Algılama yoksa boş. Arabellek boşalır.
    /// </summary>
    public short[] DrainFromWake()
    {
        lock (_lock)
        {
            var from = _detectedAt < 0 ? long.MaxValue : _detectedAt - WakeLeadSamples;
            _detectedAt = -1;
            return _preRoll.DrainFrom(from);
        }
    }

    public void Dispose()
    {
        Stop();
        _spotter.Dispose();
    }

    private void OnSamples(short[] samples)
    {
        var detected = false;
        lock (_lock)
        {
            if (_stream is null)
            {
                return;
            }

            _preRoll.Write(samples);
            var floats = new float[samples.Length];
            for (var i = 0; i < samples.Length; i++)
            {
                floats[i] = samples[i] / 32768f;
            }

            _stream.AcceptWaveform(16000, floats);
            while (_spotter.IsReady(_stream))
            {
                _spotter.Decode(_stream);
                if (!string.IsNullOrEmpty(_spotter.GetResult(_stream).Keyword))
                {
                    _detectedAt = _preRoll.Total;
                    _spotter.Reset(_stream);
                    detected = true;
                }
            }
        }

        if (detected)
        {
            Detected?.Invoke();
        }
    }
}
