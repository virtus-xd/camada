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
/// "Hey Ada, sesi kıs" tek nefeste söylenince komut kaybolmasın.
/// </summary>
internal sealed class WakeWordListener : IDisposable
{
    private const string Encoder = "encoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx";
    private const string Decoder = "decoder-epoch-12-avg-2-chunk-16-left-64.int8.onnx";
    private const string Joiner = "joiner-epoch-12-avg-2-chunk-16-left-64.int8.onnx";

    private readonly KeywordSpotter _spotter;
    private readonly Func<MMDevice?> _mic;
    private readonly PreRollBuffer _preRoll = new(32000);
    private readonly object _lock = new();
    private PlainMic? _capture;
    private OnlineStream? _stream;

    public WakeWordListener(string modelDir, double threshold, Func<MMDevice?> mic)
    {
        _mic = mic;
        var keywords = WakeWord.KeywordLine(threshold);
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
        // KeywordsBuf C# köprüsünde ANSI'ye çevriliyor ve "▁" "?" oluyor (Plan 5 Görev 4): satır UTF-8 dosyayla verilir.
        var keywordsPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CamAda", "ada-uyandirma.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(keywordsPath)!);
        File.WriteAllText(keywordsPath, keywords + Environment.NewLine, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        config.KeywordsFile = keywordsPath;
        config.KeywordsThreshold = (float)threshold;
        config.KeywordsScore = (float)WakeWord.Boost;
        _spotter = new KeywordSpotter(config);
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

    /// <summary>Dinlemeye başlar; zaten dinliyorsa dokunmaz. Mikrofon yoksa false.</summary>
    public bool Start()
    {
        lock (_lock)
        {
            if (_capture is not null)
            {
                return true;
            }

            if (_mic() is not { } device)
            {
                return false;
            }

            _stream = _spotter.CreateStream();
            _capture = new PlainMic(device);
            _capture.Samples += OnSamples;
            _capture.Failed += message => Failed?.Invoke(message);
            _capture.Start();
            return true;
        }
    }

    /// <summary>Dinlemeyi bırakır ve mikrofonu serbest bırakır (oturum mikrofonu kendisi açar).</summary>
    public void Stop()
    {
        lock (_lock)
        {
            _capture?.Dispose();
            _capture = null;
            _stream?.Dispose();
            _stream = null;
        }
    }

    /// <summary>Son 2 sn'lik ön kayıt (16 kHz mono), eskiden yeniye; arabellek boşalır.</summary>
    public short[] DrainPreRoll()
    {
        lock (_lock)
        {
            return _preRoll.Drain();
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
