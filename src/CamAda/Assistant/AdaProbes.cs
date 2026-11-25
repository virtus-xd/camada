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

        if (args.Contains("--ada-uyandirma-sina"))
        {
            await WakeAsync(Seconds(args, 60));
            return true;
        }

        return false;
    }

    /// <summary>Ayarlar; <c>CAMADA_ADA_MIC</c> verilmişse mikrofon ondan (kullanıcının ayar dosyasına dokunmadan).</summary>
    /// <summary>"--sure=N" (saniye) verilmişse o, yoksa varsayılan.</summary>
    private static int Seconds(string[] args, int fallback) =>
        args.FirstOrDefault(a => a.StartsWith("--sure=", StringComparison.Ordinal)) is { } s && int.TryParse(s[7..], out var n) ? n : fallback;

    private static string ModelDir => Path.Combine(AppContext.BaseDirectory, "Assets", "WakeWord");

    /// <summary>"Hey Ada" dinleyicisi: algılamalar ve 15 sn'de bir tek çekirdek işlemci yüzdesi.</summary>
    private static async Task WakeAsync(int seconds)
    {
        var settings = Settings();
        using var listener = new WakeWordListener(ModelDir, settings.WakeThreshold, () => MicDevices.ResolveMic(settings.Microphone));
        var count = 0;
        listener.Detected += () => Log($"ALGILANDI #{Interlocked.Increment(ref count)}");
        listener.Failed += message => Log("uyandırma hata: " + message);
        if (!listener.Start())
        {
            Log("ada-uyandirma-sina: mikrofon yok");
            return;
        }

        Log($"ada-uyandirma-sina: dinliyor ({seconds} sn, eşik {settings.WakeThreshold}, mikrofon {MicDevices.ResolveMic(settings.Microphone)?.FriendlyName})");
        var process = System.Diagnostics.Process.GetCurrentProcess();
        var end = DateTime.UtcNow.AddSeconds(seconds);
        while (DateTime.UtcNow < end)
        {
            var cpuBefore = process.TotalProcessorTime;
            var wallBefore = DateTime.UtcNow;
            var wait = TimeSpan.FromSeconds(Math.Min(15, (end - DateTime.UtcNow).TotalSeconds));
            await Task.Delay(wait);
            process.Refresh();
            var cpu = (process.TotalProcessorTime - cpuBefore).TotalMilliseconds / (DateTime.UtcNow - wallBefore).TotalMilliseconds * 100;
            Log($"İŞLEMCİ %{cpu:F1} (tek çekirdek)");
        }

        listener.Stop();
        Log($"ada-uyandirma-sina: bitti, {count} algılama");
    }

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
