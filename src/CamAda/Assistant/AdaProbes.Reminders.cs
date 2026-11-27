#if DEBUG
using System.IO;
using System.Text.Json.Nodes;
using CamAda.Core.Assistant;
using CamAda.Panels;
using CamAda.QuickSettings;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// Hatırlatma (Plan 9 Görev 4): geçici klasörde dosya ve sahte bağlamla Ada işlevleri; bozuk dosyayla açılış; 1 dk'lık
    /// hatırlatmayla zamanlayıcının bir kez "doldu" demesi. Google'a hiçbir şey gitmez, kullanıcının gerçek dosyasına dokunulmaz.
    /// </summary>
    private static async Task RemindersTrialAsync(string[] args)
    {
        if (args.Contains("--ucuca"))
        {
            await RemindersEndToEndAsync();
            return;
        }

        var dir = Directory.CreateTempSubdirectory("camada-hatirlatma-").FullName;
        try
        {
            var file = new ReminderFile(dir, TimeProvider.System);
            var context = new AdaToolContext(null, null, null, null, null, new AppIndex(), new SystemQuickLauncher(), PowerActions.Create(),
                () => [], new MemoryFile(dir, TimeProvider.System), new CurrentInfoClient(() => null, () => ""), () => Settings(),
                () => { }, _ => { }) { Reminders = file };
            var runner = new AdaToolRunner(context, TimeProvider.System);
            async Task Call(string name, string json) =>
                Log($"{name} {json} → {await runner.RunAsync(new ToolCall(null, name, JsonNode.Parse(json)!.AsObject()))}");

            Log("ada-hatirlatma-sina: başladı");
            await Call(AdaTools.HatirlaticiKur, """{"metin":"deneme","dakika":2,"saat":"","tarih":""}""");
            await Call(AdaTools.HatirlaticiKur, """{"metin":"gece","dakika":0,"saat":"23:59","tarih":""}""");
            await Call(AdaTools.HatirlaticiKur, """{"metin":"bozuk","dakika":0,"saat":"25:00","tarih":""}""");
            await Call(AdaTools.HatirlaticilariListele, "{}");
            await Call(AdaTools.HatirlaticiIptal, """{"metin":"deneme"}""");
            await Call(AdaTools.HatirlaticiIptal, """{"metin":"yok böyle"}""");
            await Call(AdaTools.HatirlaticiErtele, """{"dakika":5}""");
            await Call(AdaTools.HatirlaticiIptal, """{"metin":"hepsi"}""");
            await Call(AdaTools.HatirlaticilariListele, "{}");

            // Yeniden açılış: kayıtlı hatırlatma geri gelir; bozuk dosya yedeklenir.
            file.Add("kalıcı", DateTimeOffset.Now.AddHours(1));
            Log($"yeniden açılış: {new ReminderFile(dir, TimeProvider.System).Book.Pending.Count} bekleyen");
            var path = Path.Combine(dir, "ada-hatirlatmalar.bin");
            await File.WriteAllBytesAsync(path, [1, 2, 3, 4]);
            var broken = new ReminderFile(dir, TimeProvider.System);
            Log($"bozuk dosya: {broken.Book.Pending.Count} bekleyen, yedek {(File.Exists(Path.Combine(dir, "ada-hatirlatmalar.bozuk.bin")) ? "var" : "YOK")}");

            if (!args.Contains("--hizli"))
            {
                var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
                var timed = new ReminderFile(dir, TimeProvider.System);
                timed.Add("bir dakika", DateTimeOffset.Now.AddMinutes(1));
                using var scheduler = new ReminderScheduler(timed, dispatcher);
                var started = DateTime.UtcNow;
                var fired = 0;
                scheduler.Due += due =>
                {
                    fired++;
                    Log($"doldu ({(DateTime.UtcNow - started).TotalSeconds:F0} sn): {ReminderText.Announcement(due, DateTimeOffset.Now)}");
                };
                scheduler.Start();
                await Task.Delay(TimeSpan.FromSeconds(70));
                Log($"zamanlayıcı: {fired} kez doldu");
            }
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    /// <summary>
    /// Uçtan uca (Plan 9 Görev 7): gerçek zamanlayıcı + Ada'nın duyurusu, 1 dk'lık hatırlatma; geri sayım metinleri değiştikçe ve
    /// dolunca tek uyarı yazılır. Ada başlatılmaz (mikrofon açılmaz); söyleme oturumu yalnız yazı gönderir.
    /// </summary>
    private static async Task RemindersEndToEndAsync()
    {
        var dir = Directory.CreateTempSubdirectory("camada-hatirlatma-").FullName;
        try
        {
            var dispatcher = System.Windows.Threading.Dispatcher.CurrentDispatcher;
            var keys = new GeminiKeyStore(LocalDir);
            var settings = Settings() with { Enabled = true };
            var memory = new MemoryFile(dir, TimeProvider.System);
            var tools = new AdaToolContext(null, null, null, null, null, new AppIndex(), new SystemQuickLauncher(), PowerActions.Create(),
                () => [], memory, new CurrentInfoClient(keys.Load, () => settings.LookupModel), () => settings, () => { }, _ => { });
            using var ada = new AdaAssistant(dispatcher, new AdaDependencies(() => settings, keys, tools, memory, new UsageFile(LocalDir),
                ModelDir, ex => Log("hata günlüğü: " + ex.Message), null));
            ada.Trace += text => Log("iz: " + text);

            var file = new ReminderFile(dir, TimeProvider.System);
            file.Add("uçtan uca", DateTimeOffset.Now.AddMinutes(1));
            using var scheduler = new ReminderScheduler(file, dispatcher);
            string? lastCountdown = null;
            var alerts = 0;
            scheduler.Tick += () =>
            {
                var next = file.Book.Pending.FirstOrDefault();
                var countdown = next is null ? null : ReminderText.Countdown(next.At, DateTimeOffset.Now, System.Globalization.CultureInfo.CurrentUICulture);
                if (countdown != lastCountdown && (countdown is null || countdown.EndsWith('0') || lastCountdown is null))
                {
                    Log($"geri sayım: {countdown ?? "(yok)"}");
                }

                lastCountdown = countdown;
            };
            scheduler.Due += async due =>
            {
                alerts++;
                var sentence = ReminderText.Announcement(due, DateTimeOffset.Now);
                Log($"uyarı: adada '{sentence}' (8 sn)");
                Log($"uyarı: Ada söyledi={await ada.AnnounceAsync(sentence)}");
            };
            scheduler.Start();
            await Task.Delay(TimeSpan.FromSeconds(75));
            Log($"ada-hatirlatma-sina --ucuca: {alerts} uyarı");
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }
}
#endif
