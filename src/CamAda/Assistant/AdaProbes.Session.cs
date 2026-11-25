#if DEBUG
using System.Diagnostics;
using System.IO;
using CamAda.Assistant.Audio;
using CamAda.Audio;
using CamAda.Media;
using CamAda.Panels;
using CamAda.QuickSettings;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// Konuşmadan sınanabilen oturum akışı: dinleyici başlar, oturum tetiklenir, durumlar yazılır, oturum kapanınca (konuşma
    /// yoksa 6 sn; <c>CAMADA_ADA_FAIL_AFTER</c> ile bağlantı kopunca) uyandırmanın yeniden dinlediği ve Google'a açık bağlantı
    /// kalmadığı denetlenir. Asistan ayarda kapalı olsa da bu deneme için açık sayılır.
    /// </summary>
    private static async Task SessionAsync()
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var settings = Settings() with { Enabled = true };
        var keys = new GeminiKeyStore(LocalDir);
        using var audio = new AudioService(dispatcher);
        using var media = await NowPlayingService.CreateAsync(dispatcher);
        var memory = new MemoryFile(LocalDir, TimeProvider.System);
        var usage = new UsageFile(LocalDir);
        var before = usage.Current;
        var tools = new AdaToolContext(audio, null, null, media, null, new AppIndex(), new SystemQuickLauncher(), PowerActions.Create(),
            () => [], memory, new CurrentInfoClient(keys.Load, () => settings.LookupModel), () => settings, () => { }, _ => { });
        using var ada = new AdaAssistant(dispatcher, new AdaDependencies(() => settings, keys, tools, memory, usage, ModelDir,
            ex => Log("hata günlüğü: " + ex.Message), audio));
        var started = DateTime.UtcNow;
        var idle = new TaskCompletionSource();
        var sawSession = false;
        ada.StateChanged += (_, state) =>
        {
            Log($"+{(DateTime.UtcNow - started).TotalMilliseconds,6:F0} ms durum: {state}");
            sawSession |= state is not AdaState.Idle;
            if (sawSession && state is AdaState.Idle)
            {
                idle.TrySetResult();
            }
        };
        ada.Notice += (_, text) => Log("bilgi: " + text);

        Log($"ada-oturum-sina: mikrofon {MicDevices.ResolveMic(settings.Microphone)?.FriendlyName}, " +
            $"hata sonrası={Environment.GetEnvironmentVariable("CAMADA_ADA_FAIL_AFTER") ?? "-"}");
        ada.Start();
        await Task.Delay(2500);
        Log($"başlangıçta uyandırma dinliyor={ada.IsWakeListening}");

        started = DateTime.UtcNow;
        ada.Trigger();
        ada.Trigger(); // çift basış: ikinci basış yok sayılmalı (tek oturum)
        Log($"tetiklendi; oturum var={ada.IsSessionActive}, uyandırma dinliyor={ada.IsWakeListening}");
        await Task.WhenAny(idle.Task, Task.Delay(TimeSpan.FromSeconds(30)));
        await Task.Delay(1500);
        Log($"kapanıştan sonra: oturum var={ada.IsSessionActive}, uyandırma dinliyor={ada.IsWakeListening}, " +
            $"açık TCP={EstablishedTcp()}");
        var after = usage.Current;
        Log($"kullanım: oturum {before?.Sessions ?? 0}→{after?.Sessions ?? 0}, tahmini ${after?.EstimatedUsd - (before?.EstimatedUsd ?? 0):F4}");
        ada.Stop();
        Log("ada-oturum-sina: bitti");
    }

    private static string EstablishedTcp()
    {
        try
        {
            using var shell = Process.Start(new ProcessStartInfo("powershell.exe",
                $"-NoProfile -Command \"@(Get-NetTCPConnection -OwningProcess {Environment.ProcessId} -State Established -ErrorAction SilentlyContinue).Count\"")
            {
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            })!;
            var output = shell.StandardOutput.ReadToEnd().Trim();
            shell.WaitForExit(10000);
            return output;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or IOException)
        {
            return "ölçülemedi";
        }
    }
}
#endif
