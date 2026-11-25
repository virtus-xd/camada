#if DEBUG
using CamAda.Assistant.Audio;
using CamAda.Audio;
using CamAda.Media;
using CamAda.Panels;
using CamAda.QuickSettings;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// Gerçek oturum, gerçek mikrofon olmadan: <c>CAMADA_ADA_FAKE_ROOM</c>'daki WAV'ı Ada'nın bütün mikrofonları "duyar"
    /// (uyandırma da oturum da). Durumlar, bilgiler ve iç olaylar oda saatine göre yazılır. Seçenekler: <c>--tetik=sn</c>
    /// (o saniyede kısayol), <c>--oturum=N</c> (N oturum bitince dur), <c>--sure=sn</c>, <c>--en-uzun-dk</c>,
    /// <c>--sessizlik</c>, <c>--konusmasiz</c> (ayarı yalnız bu deneme için değiştirir).
    /// </summary>
    private static async Task RoomAsync(string[] args)
    {
        if (!FakeRoom.Active)
        {
            Log("ada-oda-sina: CAMADA_ADA_FAKE_ROOM yok (gerçek mikrofon açılmaz, deneme çıkıyor)");
            return;
        }

        int Option(string name, int fallback) =>
            args.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal)) is { } s && int.TryParse(s[(name.Length + 3)..], out var n) ? n : fallback;

        var basis = Settings();
        var settings = basis with
        {
            Enabled = true,
            MaxSessionMinutes = Option("en-uzun-dk", basis.MaxSessionMinutes),
            SilenceSeconds = Option("sessizlik", basis.SilenceSeconds),
            NoSpeechSeconds = Option("konusmasiz", basis.NoSpeechSeconds),
        };
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
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

        string At() => $"{FakeRoom.Now() / 16000.0,5:F1} sn";
        var sessions = 0;
        var wanted = Option("oturum", 1);
        var done = new TaskCompletionSource();
        var inSession = false;
        ada.StateChanged += (_, state) =>
        {
            Log($"{At()} durum: {state}");
            if (state is not AdaState.Idle)
            {
                inSession |= ada.IsSessionActive;
            }
            else if (inSession && !ada.IsSessionActive)
            {
                inSession = false;
                if (++sessions >= wanted)
                {
                    done.TrySetResult();
                }
            }
        };
        ada.Notice += (_, text) => Log($"{At()} bilgi: {text}");
        ada.Trace += text => Log($"{At()} iz: {text}");

        Log($"ada-oda-sina: en uzun {settings.MaxSessionMinutes} dk, sessizlik {settings.SilenceSeconds} sn, konuşmasız {settings.NoSpeechSeconds} sn");
        ada.Start();
        var triggers = args.FirstOrDefault(a => a.StartsWith("--tetik=", StringComparison.Ordinal))?[8..].Split(',').Select(int.Parse).ToList() ?? [];
        _ = Task.Run(async () =>
        {
            foreach (var trigger in triggers)
            {
                while (FakeRoom.Now() < trigger * 16000L)
                {
                    await Task.Delay(50);
                }

                await dispatcher.InvokeAsync(() =>
                {
                    Log($"{At()} kısayol");
                    ada.Trigger();
                });
            }
        });

        await Task.WhenAny(done.Task, Task.Delay(TimeSpan.FromSeconds(Option("sure", 60))));
        ada.Stop();
        await Task.Delay(1500);
        var after = usage.Current;
        Log($"ada-oda-sina: bitti, {sessions} oturum; açık TCP=[{EstablishedTcp()}]; kullanım: oturum {before?.Sessions ?? 0}→{after?.Sessions ?? 0}, " +
            $"tahmini ${after?.EstimatedUsd - (before?.EstimatedUsd ?? 0):F4}");
    }
}
#endif
