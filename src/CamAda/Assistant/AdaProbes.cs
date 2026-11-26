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
internal static partial class AdaProbes
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

        if (args.Contains("--ada-islev-sina"))
        {
            var at = Array.IndexOf(args, "spotify");
            if (args.Contains("misafir"))
            {
                await GuestToolCallsAsync();
            }
            else if (at >= 0)
            {
                await SpotifyAsync(at + 1 < args.Length ? args[at + 1] : "Tarkan");
            }
            else
            {
                await ToolsAsync();
            }
            return true;
        }

        if (args.Contains("--ada-oturum-sina"))
        {
            await SessionAsync();
            return true;
        }

        if (args.Contains("--ada-baglanti-sina"))
        {
            await ConnectionAsync();
            return true;
        }

        if (args.Contains("--ada-uyandirma-sina"))
        {
            await WakeAsync(Seconds(args, 60));
            return true;
        }

        if (args.Contains("--ada-kisma-sina"))
        {
            DuckProbe();
            return true;
        }

        if (args.FirstOrDefault(a => a.StartsWith("--ada-not-temizle=", StringComparison.Ordinal)) is { } leftover)
        {
            var name = leftover["--ada-not-temizle=".Length..];
            if (!System.Text.RegularExpressions.Regex.IsMatch(name, @"^Not \d\d\.\d\d\.\d{4} \d\d\.\d\d( \(\d+\))?\.txt$"))
            {
                Log("ada-not-temizle: yalnız Ada not dosyası adı (klasörsüz)");
                return true;
            }

            var path = Path.Combine(NotepadWriter.NotesFolder, name);
            await CloseOwnNoteAsync(path);
            if (Directory.Exists(NotepadWriter.NotesFolder) && !Directory.EnumerateFileSystemEntries(NotepadWriter.NotesFolder).Any())
            {
                Directory.Delete(NotepadWriter.NotesFolder);
            }

            return true;
        }

        if (args.Contains("--ada-ekran-saydam-sina"))
        {
            ScreenAlphaProbe();
            return true;
        }

        if (args.Contains("--ada-ekran-sina"))
        {
            ScreenProbe();
            return true;
        }

        if (args.Contains("--ada-not-sina"))
        {
            await NoteProbeAsync();
            return true;
        }

        if (args.Contains("--ada-esmik-sina"))
        {
            await OverlapAsync();
            return true;
        }

        if (args.Contains("--ada-hava-sina"))
        {
            await WeatherTrialAsync();
            return true;
        }

        if (args.Contains("--ada-hatirlatma-sina"))
        {
            await RemindersTrialAsync(args);
            return true;
        }

        if (args.Contains("--ada-tanitma-sina"))
        {
            await EnrollTrialAsync(args);
            return true;
        }

        if (args.Contains("--ada-suzgec-sina"))
        {
            await FilterTrialAsync(args);
            return true;
        }

        if (args.Contains("--ada-konusmaci-sina"))
        {
            await SpeakerTrialAsync(args);
            return true;
        }

        if (args.Contains("--ada-oda-sina"))
        {
            await RoomAsync(args);
            return true;
        }

        if (args.Contains("--ada-hafiza-sina"))
        {
            await MemoryWriteAsync();
            return true;
        }

        if (args.Contains("--ada-takilma-sina"))
        {
            await StallAsync();
            return true;
        }

        if (args.Contains("--ada-metin-sina"))
        {
            await TextTurnAsync();
            return true;
        }

        if (args.Contains("--ada-uyandirma-stres"))
        {
            await WakeStressAsync();
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

    /// <summary>
    /// Eski düz anahtarı içeri alır; ses göndermeden Live kurulumu yapıp kapatır; gerçek bir güncel bilgi araması ve
    /// geçersiz anahtarla bir arama dener; dosyaları ve anahtar sızıntısını denetler.
    /// </summary>
    private static async Task ConnectionAsync()
    {
        var settings = Settings();
        var keys = new GeminiKeyStore(LocalDir);
        var legacy = Path.Combine(LocalDir, "gemini-anahtar.txt");
        var imported = keys.ImportLegacy(legacy);
        var key = keys.Load();
        Log($"ada-baglanti-sina: eski anahtar içeri alındı={imported}, anahtar var={key is not null}");
        if (key is null)
        {
            return;
        }

        var setup = Core.Assistant.LiveSetup.Build(new Core.Assistant.LiveSetupOptions(settings.Model, settings.Voice,
            Core.Assistant.AdaInstruction.Build(DateTime.Now, []), Core.Assistant.AdaTools.Declarations()));
        try
        {
            await using var client = await GeminiLiveClient.ConnectAsync(key, setup, CancellationToken.None);
            Log($"kurulum tamam (yol: başlık, model {settings.Model})");
            await client.CloseAsync();
        }
        catch (AssistantException ex)
        {
            Log($"kurulum başarısız: {ex.Error}");
        }

        // Ada panelindeki "Kaydet" bu yolu kullanır: yanlış anahtar "Gemini anahtarı geçersiz" demeli.
        try
        {
            await using var badClient = await GeminiLiveClient.ConnectAsync("x", setup, CancellationToken.None);
            Log("geçersiz anahtarla Live kurulumu: BEKLENMEDİK başarı");
        }
        catch (AssistantException ex)
        {
            Log($"geçersiz anahtarla Live kurulumu: {ex.Error}");
        }

        var info = new CurrentInfoClient(() => key, () => settings.LookupModel);
        var answer = await info.AskAsync("Bugün 1 ABD doları kaç Türk lirası?", CancellationToken.None);
        Log($"arama: Ok={answer.Ok}, Searched={answer.Searched}, tokenlar {answer.PromptTokens}/{answer.OutputTokens}, " +
            $"hata {answer.Error}: {answer.Text.ReplaceLineEndings(" ")[..Math.Min(160, answer.Text.Length)]}");

        var bad = await new CurrentInfoClient(() => "x", () => settings.LookupModel).AskAsync("deneme", CancellationToken.None);
        Log($"geçersiz anahtar: {bad.Error}");

        Log($"dosyalar: gemini-anahtar.txt var={File.Exists(legacy)}, gemini-key.bin var={File.Exists(Path.Combine(LocalDir, "gemini-key.bin"))}");
        var probe = key[..Math.Min(8, key.Length)];
        var leaked = new[] { "ada-deneme.log", "crash.log" }
            .Select(f => Path.Combine(LocalDir, f))
            .Where(File.Exists)
            .Any(f => File.ReadAllText(f).Contains(probe, StringComparison.Ordinal));
        Log($"anahtar sızıntısı: {(leaked ? "VAR" : "yok")}");
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
