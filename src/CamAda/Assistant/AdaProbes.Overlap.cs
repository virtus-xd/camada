#if DEBUG
using System.Diagnostics;
using CamAda.Assistant.Audio;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// Uyandırma dinleyicisinin düz mikrofonu açıkken yankı engelleyici açılıyor mu (oturum başındaki köprü): ikisi 3 sn
    /// birlikte, sonra düz mikrofon kapanır ve yankı engelleyici 1 sn daha sürer. Ses süreçten çıkmaz; yalnız bayt sayısı,
    /// tepe ve ilk veri süresi yazılır.
    /// </summary>
    private static async Task OverlapAsync()
    {
        var mic = MicDevices.ResolveMic(Settings().Microphone);
        var speaker = MicDevices.DefaultSpeaker();
        if (mic is null || speaker is null)
        {
            Log("ada-esmik-sina: cihaz yok");
            return;
        }

        var (capture, render) = MicDevices.DspIndexes(mic, speaker);
        Log($"ada-esmik-sina: {mic.FriendlyName}: sessiz={mic.AudioEndpointVolume.Mute}, düzey %{mic.AudioEndpointVolume.MasterVolumeLevelScalar * 100:F0}");
        long plainBytes = 0, aecBytes = 0, aecAfterPlainStopped = 0;
        double plainPeak = 0, aecPeak = 0, plainPeakAlone = 0;
        double plainRmsAlone = 0, plainRmsTogether = 0;
        int aloneChunks = 0, togetherChunks = 0;
        var aecFirst = TimeSpan.Zero;
        string? aecFailure = null;
        var plainStopped = false;
        var aecStarted = false;
        var clock = Stopwatch.StartNew();

        using var plain = new PlainMic(mic);
        plain.Samples += s =>
        {
            Interlocked.Add(ref plainBytes, s.Length * 2);
            var peak = s.Max(v => Math.Abs(v / 32768.0));
            var rms = Math.Sqrt(s.Average(v => (v / 32768.0) * (v / 32768.0)));
            if (aecStarted)
            {
                plainPeak = Math.Max(plainPeak, peak);
                plainRmsTogether += rms;
                togetherChunks++;
            }
            else
            {
                plainPeakAlone = Math.Max(plainPeakAlone, peak);
                plainRmsAlone += rms;
                aloneChunks++;
            }
        };
        plain.Start();
        await Task.Delay(1500); // dinleyici çoktan açık

        var aecStart = clock.Elapsed;
        using var aec = new EchoCancelledMic(capture, render);
        aec.Samples += s =>
        {
            if (aecFirst == TimeSpan.Zero)
            {
                aecFirst = clock.Elapsed - aecStart;
            }

            Interlocked.Add(ref aecBytes, s.Length * 2);
            if (plainStopped)
            {
                Interlocked.Add(ref aecAfterPlainStopped, s.Length * 2);
            }

            aecPeak = Math.Max(aecPeak, s.Max(v => Math.Abs(v / 32768.0)));
        };
        aec.Failed += message => aecFailure = message;
        aecStarted = true;
        aec.Start();
        await Task.Delay(3000);
        plain.Dispose();
        plainStopped = true;
        await Task.Delay(1000);

        Log($"ada-esmik-sina: düz mikrofon yalnızken tepe {plainPeakAlone:F4} RMS {plainRmsAlone / Math.Max(1, aloneChunks):F5}, " +
            $"birlikteyken tepe {plainPeak:F4} RMS {plainRmsTogether / Math.Max(1, togetherChunks):F5}, {plainBytes} bayt; yankı engelleyici ilk veri {aecFirst.TotalMilliseconds:F0} ms, " +
            $"{aecBytes} bayt (tepe {aecPeak:F3}), düz kapandıktan sonra {aecAfterPlainStopped} bayt, hata {aecFailure ?? "yok"}");
    }
}
#endif
