#if DEBUG
using System.IO;
using System.Text.Json.Nodes;
using CamAda.Audio;
using CamAda.Core.Assistant;
using CamAda.Core.Media;
using CamAda.Media;
using CamAda.Panels;
using CamAda.QuickSettings;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// İşlevleri gerçek servislerle çağırır ve sonuçları yazar; dokunduğu her durumu (ses, cihaz, parlaklık, medya) geri
    /// yükler. Kapatma yalnız <c>CAMADA_FAKE_POWER</c> ile denenir; Not Defteri yalnız deneme açtıysa kapatılır.
    /// </summary>
    private static async Task ToolsAsync()
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var settings = Settings();
        var key = new GeminiKeyStore(LocalDir).Load();
        using var audio = new AudioService(dispatcher);
        using var brightness = new BrightnessService(dispatcher, TimeProvider.System);
        await brightness.RefreshAsync();
        using var radios = await RadioService.CreateAsync(dispatcher);
        using var media = await NowPlayingService.CreateAsync(dispatcher);
        var apps = new AppIndex();
        var appItems = await apps.GetItemsAsync();
        var power = PowerActions.Create();
        var context = new AdaToolContext(audio, brightness, radios, media, null, apps, new SystemQuickLauncher(), power,
            () => ProbeWindows.List(appItems), new MemoryFile(LocalDir, TimeProvider.System),
            new CurrentInfoClient(() => key, () => settings.LookupModel), () => settings,
            () => Log("  (oturum konuşmadan sonra bitecek)"),
            r => Log($"  (arama kullanıldı: aranan={r.Searched}, tokenlar {r.PromptTokens}/{r.OutputTokens})"));
        var runner = new AdaToolRunner(context, TimeProvider.System);

        async Task<string> Call(string name, string json = "{}")
        {
            var result = await runner.RunAsync(new ToolCall(null, name, JsonNode.Parse(json)!.AsObject()));
            Log($"{name} {json} → {result.ReplaceLineEndings(" ")[..Math.Min(220, result.Length)]}");
            return result;
        }

        Log("ada-islev-sina: başladı");

        // Ses ve bozuk argümanlar: önce kayıt, sonra geri.
        var volume = audio.Volume;
        var muted = audio.Muted;
        await Call(AdaTools.SesAyarla, """{"yuzde":35}""");
        await Call(AdaTools.SesDegistir, """{"fark":-5}""");
        await Call(AdaTools.SesAyarla);
        await Call(AdaTools.SesAyarla, """{"yuzde":"40"}""");
        audio.SetVolume(volume);
        audio.SetMuted(muted);
        Log($"ses geri: %{Math.Round(audio.Volume * 100)} sessiz={audio.Muted}");
        await Call("yok_islev");

        // Çıkış cihazı.
        var output = audio.Outputs().FirstOrDefault(o => o.IsDefault);
        await Call(AdaTools.CikisCihaziSec, """{"ad":"hoparlör"}""");
        await Call(AdaTools.CikisCihaziSec, """{"ad":"yok cihaz"}""");
        await Call(AdaTools.CikisCihaziSec, """{"ad":"sonar"}""");
        if (output is not null && audio.Outputs().FirstOrDefault(o => o.IsDefault)?.Id != output.Id)
        {
            audio.SetDefaultOutput(output.Id);
        }

        await Task.Delay(500);
        Log($"çıkış geri: {audio.Outputs().FirstOrDefault(o => o.IsDefault)?.Name}; girişler: " +
            string.Join(" | ", audio.Inputs().Select(i => i.Name + (i.IsDefault ? " *" : ""))));

        // Parlaklık.
        var display = brightness.Displays.FirstOrDefault(d => d.IsInternal) ?? brightness.Displays.FirstOrDefault();
        if (display is not null)
        {
            await Call(AdaTools.ParlaklikAyarla, """{"yuzde":60}""");
            await Task.Delay(1500);
            brightness.Set(display.Id, display.Level);
            await Task.Delay(1500);
            await brightness.RefreshAsync();
            Log($"parlaklık geri: %{brightness.Displays.FirstOrDefault(d => d.Id == display.Id)?.Level} (önce %{display.Level})");
        }
        else
        {
            Log("parlaklık: ekran yok, atlandı");
        }

        // Medya: yalnız çalan bir şey varsa duraklat, sonra eski durum.
        if (media.Current?.State == PlaybackState.Playing)
        {
            await Call(AdaTools.MedyaKontrol, """{"komut":"duraklat"}""");
            await Task.Delay(1500);
            await Call(AdaTools.MedyaKontrol, """{"komut":"oynat"}""");
        }
        else
        {
            Log($"medya: çalan yok ({media.Current?.State.ToString() ?? "oturum yok"}), atlandı");
        }

        // Uygulamalar: yalnız denemenin açtığı Not Defteri kapatılır.
        static bool IsNotepad(OpenWindow w) => w.ExeName.Equals("notepad", StringComparison.OrdinalIgnoreCase);
        if (ProbeWindows.List(appItems).Any(IsNotepad))
        {
            Log("uygulamalar: açık Not Defteri var, atlandı");
        }
        else
        {
            await Call(AdaTools.UygulamaAc, """{"ad":"Not Defteri"}""");
            for (var i = 0; i < 20 && !ProbeWindows.List(appItems).Any(IsNotepad); i++)
            {
                await Task.Delay(250);
            }

            var opened = ProbeWindows.List(appItems).Where(IsNotepad).ToList();
            Log($"Not Defteri pencereleri: {string.Join(" | ", opened.Select(w => $"{w.AppName}/{w.ExeName}/{w.Title}"))}");
            if (opened.Count > 0)
            {
                await Call(AdaTools.UygulamaKapat, """{"ad":"Not Defteri"}""");
                await Task.Delay(1500);
                Log($"kapandı: {!ProbeWindows.List(appItems).Any(IsNotepad)}");
            }
        }

        await Call(AdaTools.UygulamaKapat, """{"ad":"olmayan uygulama"}""");
        await Call(AdaTools.UygulamaAc, """{"ad":"olmayan uygulama"}""");

        // Tarayıcı, güncel bilgi, WhatsApp.
        await Call(AdaTools.WebSitesiAc, """{"adres":"youtube"}""");
        await Call(AdaTools.WebSitesiAc, """{"adres":"javascript:alert(1)"}""");
        await Call(AdaTools.GuncelBilgi, """{"soru":"gram altın kaç TL"}""");
        await Call(AdaTools.WhatsAppMesaj, """{"kisi":"Yok Kişi","mesaj":"x"}""");

        // Güç: yalnız sahtesiyle.
        if (power is RecordingPowerActions)
        {
            await Call(AdaTools.BilgisayariKapat, """{"yeniden_baslat":false}""");
            await Call(AdaTools.BilgisayariKapat, """{"yeniden_baslat":false}""");
            Log("sahte güç günlüğü: " + File.ReadAllText(Environment.GetEnvironmentVariable("CAMADA_FAKE_POWER")!).ReplaceLineEndings(" ").Trim());
        }
        else
        {
            Log("güç: CAMADA_FAKE_POWER yok, atlandı");
        }

        // Hafıza ve henüz bağlanmamış işlev.
        await Call(AdaTools.Hatirla, """{"konu":"deneme","bilgi":"silinecek"}""");
        await Call(AdaTools.Hatirla, """{"konu":"kart","bilgi":"kartım 4111 1111 1111 1111"}""");
        await Call(AdaTools.Unut, """{"konu":"deneme"}""");
        await Call(AdaTools.Unut, """{"konu":"deneme"}""");
        await Call(AdaTools.SpotifyCal, """{"sorgu":"Tarkan"}""");
        Log("ada-islev-sina: bitti");
    }

    /// <summary>
    /// "spotify_cal" gerçek Spotify ile: önceki durum kaydedilir; çalma denenir; önceden çalan bir şey yoksa sonra duraklatılır.
    /// </summary>
    private static async Task SpotifyAsync(string query)
    {
        var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
        var all = new Core.Settings.SettingsStore(RoamingDir).Load();
        var spotify = new Spotify.SpotifyClient(() => all.SpotifyClientId, new Spotify.SpotifyTokenStore(LocalDir), TimeProvider.System);
        Log($"ada-islev-sina spotify: bağlı={spotify.IsConnected}, izin yenilenmeli={spotify.NeedsReconnect}");
        using var media = await NowPlayingService.CreateAsync(dispatcher);
        var wasPlaying = media.Current?.State == PlaybackState.Playing;
        var context = new AdaToolContext(null, null, null, media, spotify, new AppIndex(), new SystemQuickLauncher(), PowerActions.Create(),
            () => [], new MemoryFile(LocalDir, TimeProvider.System), new CurrentInfoClient(() => null, () => ""), () => all.Assistant,
            () => { }, _ => { });
        var runner = new AdaToolRunner(context, TimeProvider.System);
        var started = DateTime.UtcNow;
        var result = await runner.RunAsync(new ToolCall(null, AdaTools.SpotifyCal, new JsonObject { ["sorgu"] = query }));
        Log($"spotify_cal {query} → {result} ({(DateTime.UtcNow - started).TotalMilliseconds:F0} ms)");
        if (result.StartsWith("tamam", StringComparison.Ordinal))
        {
            await Task.Delay(6000);
            Log($"şimdi çalan: {media.Current?.Artist} — {media.Current?.Title} ({media.Current?.State})");
            if (!wasPlaying && media.Current?.State == PlaybackState.Playing)
            {
                await media.TogglePlayPauseAsync();
                await Task.Delay(800);
                Log($"önceki durum geri: {media.Current?.State}");
            }
        }
    }
}
#endif
