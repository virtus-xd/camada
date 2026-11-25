using CamAda.Core.Audio;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace CamAda.Assistant.Audio;

/// <summary>
/// Yankı engelsiz mikrofon: cihazın kendi biçimi (ör. 48 kHz stereo float) 16 kHz mono'ya çevrilir. Uyandırma
/// dinleyicisi ve yankı engelleyici açılamazsa yarı çift yönlü yedek bununla çalışır.
/// </summary>
internal sealed class PlainMic : IAdaMic
{
    private const int ChunkSamples = 1600; // 100 ms
    private readonly WasapiCapture _capture;
    private readonly Downsampler16k _downsampler;
    private readonly List<short> _pending = new(ChunkSamples * 2);
    private readonly int _bytesPerSample;
    private bool _disposed;

    public PlainMic(MMDevice device)
    {
        _capture = new WasapiCapture(device) { ShareMode = AudioClientShareMode.Shared };
        var format = _capture.WaveFormat;
        _bytesPerSample = format.BitsPerSample / 8;
        _downsampler = new Downsampler16k(format.SampleRate, format.Channels);
        _capture.DataAvailable += OnData;
        _capture.RecordingStopped += (_, e) =>
        {
            if (e.Exception is not null && !_disposed)
            {
                Failed?.Invoke(e.Exception.Message);
            }
        };
    }

    public event Action<short[]>? Samples;

    public event Action<string>? Failed;

    public bool EchoCancelled => false;

    public void Start()
    {
#if DEBUG
        if (Environment.GetEnvironmentVariable("CAMADA_ADA_MIC_DENIED") == "1")
        {
            // Windows gizlilik ayarı masaüstü uygulamalarına mikrofonu kapatınca WASAPI E_ACCESSDENIED verir.
            throw new UnauthorizedAccessException("deneme: mikrofon izni kapalı sayıldı");
        }
#endif
        _capture.StartRecording();
    }

    public void Dispose()
    {
        _disposed = true;
        try
        {
            _capture.StopRecording();
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.Runtime.InteropServices.COMException)
        {
        }

        _capture.Dispose();
    }

    private void OnData(object? sender, WaveInEventArgs e)
    {
        var count = e.BytesRecorded / _bytesPerSample;
        var floats = new float[count];
        for (var i = 0; i < count; i++)
        {
            floats[i] = _bytesPerSample == 4
                ? BitConverter.ToSingle(e.Buffer, i * 4)
                : BitConverter.ToInt16(e.Buffer, i * 2) / 32768f;
        }

        _pending.AddRange(_downsampler.Process(floats));
        while (_pending.Count >= ChunkSamples)
        {
            Samples?.Invoke(_pending.GetRange(0, ChunkSamples).ToArray());
            _pending.RemoveRange(0, ChunkSamples);
        }
    }
}
