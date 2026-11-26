#if DEBUG
using System.IO;
using CamAda.Assistant.Audio;
using CamAda.Core.Assistant;
using CamAda.Interop;
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
        // Misafir kipi (Plan 8): ses izi verilen WAV'dan, kullanıcının gerçek ses izi dosyasına dokunmadan geçici klasörde.
        VoiceprintFile? voiceprints = null;
        if (args.FirstOrDefault(a => a.StartsWith("--ses-izi-wav=", StringComparison.Ordinal)) is { } enrollArg)
        {
            var tempDir = Directory.CreateTempSubdirectory("camada-ses-izi-").FullName;
            voiceprints = new VoiceprintFile(tempDir);
            using var embedder = new SpeakerEmbedder(Path.Combine(VoiceModels.Dir, VoiceModels.SpeakerModel));
            var vadPath = Path.Combine(VoiceModels.Dir, VoiceModels.Vad);
            var mic = args.FirstOrDefault(a => a.StartsWith("--ses-izi-mik=", StringComparison.Ordinal))?["--ses-izi-mik=".Length..];
            var print = new Voiceprint(VoiceModels.SpeakerModel,
                Segments(ReadWav(enrollArg["--ses-izi-wav=".Length..]), vadPath).Where(s => s.Length >= 16000).Select(s => embedder.Compute(s)).ToList(),
                mic);
            voiceprints.Save(print);
            Log($"ses izi: {print.Samples.Count} parçadan, geçici klasörde");
            foreach (var scoreArg in args.Where(a => a.StartsWith("--puanla=", StringComparison.Ordinal)))
            {
                var file = scoreArg["--puanla=".Length..];
                var scores = Segments(ReadWav(file), vadPath).Where(s => s.Length >= 8000).Select(s => print.Score(embedder.Compute(s)).ToString("F2", Inv));
                Log($"puan {Path.GetFileName(file)} (eşik {settings.SpeakerThreshold:F2}): [{string.Join(" ", scores)}]");
            }
        }

        using var ada = new AdaAssistant(dispatcher, new AdaDependencies(() => settings, keys, tools, memory, usage, ModelDir,
            ex => Log("hata günlüğü: " + ex.Message), audio) { Voiceprint = voiceprints });

        string At() => $"{FakeRoom.Now() / 16000.0,5:F1} sn";
        ada.GuestModeChanged += (_, on) => Log($"{At()} misafir kipi: {(on ? "açık" : "kapalı")}");
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
        var readNote = args.Contains("--not-oku");
        var probeStarted = DateTime.Now;
        if (readNote)
        {
            Log($"not: Not Defteri yedeği {BackupNotepadState() ?? "yok"}; açık sekme {NotepadTabCount()}; ana ses %{Math.Round(audio.Volume * 100)}");
        }

        ada.Start();
        if (args.Contains("--misafir"))
        {
            for (var i = 0; i < 100 && !ada.IsVoiceReady; i++)
            {
                await Task.Delay(100);
            }

            Log($"misafir kipi elle: {(ada.SetGuestManual(true) ? "açıldı" : "AÇILAMADI (ses tanıma yok)")}");
        }

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
        if (readNote)
        {
            await ReadAndRemoveNoteAsync(probeStarted);
            Log($"not: ana ses %{Math.Round(audio.Volume * 100)}; açık sekme {NotepadTabCount()}");
        }

        var after = usage.Current;
        Log($"ada-oda-sina: bitti, {sessions} oturum; açık TCP=[{EstablishedTcp()}]; kullanım: oturum {before?.Sessions ?? 0}→{after?.Sessions ?? 0}, " +
            $"tahmini ${after?.EstimatedUsd - (before?.EstimatedUsd ?? 0):F4}");
    }

    /// <summary>
    /// Denemenin Ada Notları'na yazdığı notları okur (diskten ve ekrandan), yalnız kendi sekmesini kapatır (kaydedilmişse,
    /// başlık denetimiyle Ctrl+W) ve dosyayı siler. Kaydedilmemiş sekme kapatılmaz (Not Defteri sorardı).
    /// </summary>
    private static async Task ReadAndRemoveNoteAsync(DateTime since)
    {
        var folder = NotepadWriter.NotesFolder;
        var notes = Directory.Exists(folder)
            ? Directory.GetFiles(folder, "Not *.txt").Where(f => File.GetCreationTime(f) >= since.AddSeconds(-1)).ToList()
            : [];
        if (notes.Count == 0)
        {
            Log("not: Ada Notları'nda yeni not yok");
            return;
        }

        foreach (var path in notes)
        {
            var name = Path.GetFileName(path);
            var front = Native.GetForegroundWindow();
            var title = Native.TitleOf(front);
            var onScreen = title.Contains(name, StringComparison.Ordinal) ? ReadDocument(front) : null;
            Log($"not {name}: diskte \"{File.ReadAllText(path).ReplaceLineEndings("\\n")}\"; ekranda \"{onScreen?.ReplaceLineEndings("\\n")}\"; başlık \"{title}\"");
            await CloseOwnNoteAsync(path);
        }

        if (Directory.Exists(folder) && !Directory.EnumerateFileSystemEntries(folder).Any())
        {
            Directory.Delete(folder);
        }
    }
}
#endif
