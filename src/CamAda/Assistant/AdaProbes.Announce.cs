#if DEBUG
using System.Diagnostics;
using System.IO;
using CamAda.Panels;
using CamAda.QuickSettings;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// Hatırlatma duyurusu (Plan 9 Görev 6): Ada başlatılmaz (mikrofon ve "Hey Ada" açılmaz); söyleme oturumu Google'a yalnız yazı
    /// gönderir ve hoparlörden çalar. <c>--gecersiz-anahtar</c>: geçici klasörde uydurma anahtarla false dönmeli (yalnız ton).
    /// </summary>
    private static async Task AnnounceTrialAsync(string[] args)
    {
        var temp = Directory.CreateTempSubdirectory("camada-duyuru-").FullName;
        try
        {
            var keys = new GeminiKeyStore(LocalDir);
            if (args.Contains("--gecersiz-anahtar"))
            {
                keys = new GeminiKeyStore(temp);
                keys.Save("AIzaSy" + new string('x', 33));
            }

            var settings = Settings() with { Enabled = true };
            var memory = new MemoryFile(temp, TimeProvider.System);
            var tools = new AdaToolContext(null, null, null, null, null, new AppIndex(), new SystemQuickLauncher(), PowerActions.Create(),
                () => [], memory, new CurrentInfoClient(keys.Load, () => settings.LookupModel), () => settings, () => { }, _ => { });
            var usage = new UsageFile(LocalDir);
            var before = usage.Current?.EstimatedUsd ?? 0;
            using var ada = new AdaAssistant(System.Windows.Threading.Dispatcher.CurrentDispatcher,
                new AdaDependencies(() => settings, keys, tools, memory, usage, ModelDir, ex => Log("hata günlüğü: " + ex.Message), null));
            ada.Trace += text => Log("iz: " + text);
            var watch = Stopwatch.StartNew();
            var spoke = await ada.AnnounceAsync("Süre doldu: deneme.");
            await Task.Delay(1500); // geç gelen kullanım olayı
            Log($"ada-duyuru-sina: söyledi={spoke}, {watch.ElapsedMilliseconds} ms, maliyet ${(usage.Current?.EstimatedUsd ?? 0) - before:F4}");
        }
        finally
        {
            Directory.Delete(temp, recursive: true);
        }
    }
}
#endif
