#if DEBUG
using System.IO;
using CamAda.Assistant.Audio;
using CamAda.Core.Audio;
using CamAda.Core.Settings;

namespace CamAda.Assistant;

/// <summary>
/// Yalnız Debug: Ada'nın parçalarını çalışan CamAda'ya dokunmadan tek tek deneyen komut satırı girişleri. Her biri
/// sonucunu <c>%LOCALAPPDATA%\CamAda\ada-deneme.log</c>'a yazıp çıkar.
/// </summary>
internal static class AdaProbes
{
    private static readonly string LocalDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CamAda");

    private static readonly string RoamingDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CamAda");

    /// <summary>Argümanlarda bir deneme varsa çalıştırır ve true döner.</summary>
    public static async Task<bool> TryRunAsync(string[] args)
    {
        if (args.Contains("--ada-ses-sina"))
        {
            await SoundAsync();
            return true;
        }

        return false;
    }

    /// <summary>Ayarlar; <c>CAMADA_ADA_MIC</c> verilmişse mikrofon ondan (kullanıcının ayar dosyasına dokunmadan).</summary>
    private static AssistantSettings Settings()
    {
        var settings = new SettingsStore(RoamingDir).Load().Assistant;
        return Environment.GetEnvironmentVariable("CAMADA_ADA_MIC") is { Length: > 0 } mic ? settings with { Microphone = mic } : settings;
    }

    internal static void Log(string line)
    {
        Directory.CreateDirectory(LocalDir);
        File.AppendAllText(Path.Combine(LocalDir, "ada-deneme.log"), $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}");
    }

    /// <summary>Yankı engelleyicili mikrofon 3 sn (açılamazsa düz mikrofon) + uyanma bipi.</summary>
    private static async Task SoundAsync()
    {
        var mic = MicDevices.ResolveMic(Settings().Microphone);
        var speaker = MicDevices.DefaultSpeaker();
        if (mic is null || speaker is null)
        {
            Log($"ada-ses-sina: cihaz yok (mikrofon {mic?.FriendlyName ?? "-"}, hoparlör {speaker?.FriendlyName ?? "-"})");
            return;
        }

        long bytes = 0;
        double peak = 0;
        string? failure = null;
        var echoCancelled = true;
        void OnSamples(short[] samples)
        {
            Interlocked.Add(ref bytes, samples.Length * 2);
            foreach (var s in samples)
            {
                peak = Math.Max(peak, Math.Abs(s / 32768.0));
            }
        }

        using var output = new AdaSpeaker(speaker);
        var (capture, render) = MicDevices.DspIndexes(mic, speaker);
        IAdaMic active = new EchoCancelledMic(capture, render);
        var fellBack = new TaskCompletionSource();
        active.Samples += OnSamples;
        active.Failed += message =>
        {
            failure = message;
            fellBack.TrySetResult();
        };
        active.Start();
        output.PlayCue(AdaCue.Wake);

        var started = DateTime.UtcNow;
        if (await Task.WhenAny(fellBack.Task, Task.Delay(3000)) == fellBack.Task)
        {
            active.Dispose();
            echoCancelled = false;
            active = new PlainMic(mic);
            active.Samples += OnSamples;
            active.Start();
            await Task.Delay(TimeSpan.FromSeconds(3) - (DateTime.UtcNow - started) is { Ticks: > 0 } rest ? rest : TimeSpan.Zero);
        }

        active.Dispose();
        Log($"ada-ses-sina: mikrofon {mic.FriendlyName} (#{capture}), hoparlör {speaker.FriendlyName} (#{render}); " +
            $"{bytes} bayt, tepe {peak:F3}, yankı engelli {echoCancelled.ToString().ToLowerInvariant()}, " +
            $"hata {(failure is null ? "yok" : failure)}");
    }
}
#endif
