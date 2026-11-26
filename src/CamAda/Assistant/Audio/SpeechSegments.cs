using SherpaOnnx;

namespace CamAda.Assistant.Audio;

/// <summary>Kaydın yalnız konuşma kısımları (Silero, oturumdaki süzgeçle aynı ayarlar): tanıtmada gömme bunlardan hesaplanır.</summary>
internal static class SpeechSegments
{
    public static short[] Speech(short[] audio, string vadPath)
    {
        var config = new VadModelConfig();
        config.SileroVad.Model = vadPath;
        config.SileroVad.Threshold = 0.5f;
        config.SileroVad.MinSilenceDuration = 0.25f;
        config.SileroVad.MinSpeechDuration = 0.25f;
        config.SileroVad.WindowSize = 512;
        config.SileroVad.MaxSpeechDuration = 20f;
        config.SampleRate = 16000;
        config.NumThreads = 1;
        config.Provider = "cpu";
        using var vad = new VoiceActivityDetector(config, 30);
        var speech = new List<short>();
        void Drain()
        {
            while (!vad.IsEmpty())
            {
                foreach (var f in vad.Front().Samples)
                {
                    speech.Add((short)Math.Clamp(f * 32768f, short.MinValue, short.MaxValue));
                }

                vad.Pop();
            }
        }

        var window = new float[512];
        for (var at = 0; at + 512 <= audio.Length; at += 512)
        {
            for (var i = 0; i < 512; i++)
            {
                window[i] = audio[at + i] / 32768f;
            }

            vad.AcceptWaveform(window);
            Drain();
        }

        vad.Flush();
        Drain();
        return speech.ToArray();
    }
}
