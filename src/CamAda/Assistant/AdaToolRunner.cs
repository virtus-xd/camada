using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using CamAda.Core.Assistant;
using CamAda.Core.Diagnostics;
using CamAda.Core.Media;
using CamAda.Core.Search;
using CamAda.Interop;
using CamAda.Panels;
using Windows.Devices.Radios;

namespace CamAda.Assistant;

/// <summary>
/// Modelin işlev çağrılarını gerçek işlemlere çevirir. Her dönüş "tamam: …", "hata: …" ya da "onay gerekiyor: …" ile
/// başlar; model yalnız buna göre konuşur (yapılmamış işi yapıldı sanmasın diye). Arayüz iş parçacığında çağrılır
/// (servisler oraya bağlı).
/// </summary>
internal sealed class AdaToolRunner(AdaToolContext ctx, TimeProvider time)
{
    private static readonly CultureInfo Turkish = CultureInfo.GetCultureInfo("tr-TR");
    private static readonly TimeSpan ConfirmWindow = TimeSpan.FromSeconds(30);

    /// <summary>İnterneti ya da bilgisayarı kesen işlemler, Ada'nın yanıtı duyulsun diye biraz bekletilir.</summary>
    internal static readonly TimeSpan DisruptiveDelay = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Başlat menüsünde Türkçe adıyla bulunmayan Windows araçları (kısayol dosyaları İngilizce adlı:
    /// "File Explorer.lnk" gibi).
    /// </summary>
    private static readonly SearchItem[] SystemApps =
    [
        new("Dosya Gezgini", SearchKind.App, "explorer.exe"),
        new("Görev Yöneticisi", SearchKind.App, "taskmgr.exe"),
        new("Denetim Masası", SearchKind.App, "control.exe"),
        new("Komut İstemi", SearchKind.App, "cmd.exe"),
        new("Ayarlar", SearchKind.Setting, "ms-settings:"),
    ];

    private static readonly string LocalDir =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "CamAda");

    private readonly UserConfirmGate _shutdownGate = new(time, ConfirmWindow);
    private readonly UserConfirmGate _rebootGate = new(time, ConfirmWindow);
    private readonly SemaphoreSlim _noteWrites = new(1, 1); // parçalar sırayla yazılır (üst üste gelen not_yaz)
    private NoteTarget? _note;

    /// <summary>Notta geçen ya da not sınırında gelen komut yürütülmez; model söyleneni nota yazmalı.</summary>
    public const string NoteRefusal = "hata: not alınıyor; bu komut yürütülmez, söylenen olduğu gibi not_yaz ile nota yazılmalı";

    /// <summary>Not kipi (tasarım "Ada ile not alma"): AdaAssistant susturma ve 4 sn bitiş için okur.</summary>
    public NoteSession Notes { get; } = new(time, NoteSession.Quiet);

    /// <summary>Kullanıcının sözü geldi (canlı yazı): kapatma onayı yalnız sorudan sonra konuşulduysa geçer.</summary>
    public void UserSpoke()
    {
        _shutdownGate.UserSpoke();
        _rebootGate.UserSpoke();
    }

    /// <summary>Yeni oturum: önceki oturumda sorulan "emin misin?" ve açılan not geçerli değildir.</summary>
    public void ResetConfirmations()
    {
        _shutdownGate.Reset();
        _rebootGate.Reset();
        Notes.Reset();
        _note = null;
    }

    /// <summary>Not sürüyorsa bitirir (dosya her parçada zaten yazıldı); bittiyse true.</summary>
    public bool FinishNote() => Notes.Finish();

    public async Task<string> RunAsync(ToolCall call)
    {
        try
        {
            return await DispatchAsync(call.Name, call.Args);
        }
        catch (ArgException ex)
        {
            return "hata: " + ex.Message;
        }
        catch (Exception ex)
        {
            try
            {
                CrashLog.Append(LocalDir, ex, DateTimeOffset.Now);
            }
            catch (Exception log) when (log is IOException or UnauthorizedAccessException)
            {
            }

            return "hata: beklenmeyen bir sorun oldu";
        }
    }

    private Task<string> DispatchAsync(string name, JsonObject args) =>
        Notes.Refuses(name) ? Done(NoteRefusal) : RunToolAsync(name, args);

    private Task<string> RunToolAsync(string name, JsonObject args) => name switch
    {
        AdaTools.SesAyarla => Done(SetVolume(Int(args, "yuzde", 0, 100), relative: false)),
        AdaTools.SesDegistir => Done(SetVolume(Int(args, "fark", -100, 100), relative: true)),
        AdaTools.SessizeAl => Done(Mute(Bool(args, "sessiz"))),
        AdaTools.CikisCihaziSec => Done(PickDevice(Text(args, "ad"), output: true)),
        AdaTools.GirisCihaziSec => Done(PickDevice(Text(args, "ad"), output: false)),
        AdaTools.Mikrofon => Done(Microphone(Bool(args, "acik"))),
        AdaTools.ParlaklikAyarla => BrightnessAsync(Int(args, "yuzde", 0, 100), relative: false),
        AdaTools.ParlaklikDegistir => BrightnessAsync(Int(args, "fark", -100, 100), relative: true),
        AdaTools.Wifi => RadioAsync(RadioKind.WiFi, "Wi-Fi", Bool(args, "acik")),
        AdaTools.Bluetooth => RadioAsync(RadioKind.Bluetooth, "Bluetooth", Bool(args, "acik")),
        AdaTools.MedyaKontrol => MediaAsync(Text(args, "komut")),
        AdaTools.SpotifyCal => SpotifyAsync(Text(args, "sorgu")),
        AdaTools.UygulamaAc => OpenAppAsync(Text(args, "ad")),
        AdaTools.UygulamaKapat => Done(CloseApp(Text(args, "ad"))),
        AdaTools.WebSitesiAc => Done(OpenSite(Text(args, "adres"))),
        AdaTools.TarayicidaGoogleAc => Done(GoogleSearch(Text(args, "sorgu"))),
        AdaTools.GuncelBilgi => LookupAsync(Text(args, "soru")),
        AdaTools.WhatsAppMesaj => Done(WhatsApp(Text(args, "kisi"), Text(args, "mesaj"))),
        AdaTools.EkranGoruntusu => Done(Screenshot()),
        AdaTools.Kilitle => Done(Lock()),
        AdaTools.Uyku => Done(Sleep()),
        AdaTools.BilgisayariKapat => Done(Power(Bool(args, "yeniden_baslat"))),
        AdaTools.KonusmayiBitir => Done(EndConversation()),
        AdaTools.Hatirla => Done(Remember(Text(args, "konu"), Text(args, "bilgi"))),
        AdaTools.Unut => Done(Forget(Text(args, "konu"))),
        AdaTools.NotAl => TakeNoteAsync(ToolArgs.Text(args, "metin").Value ?? ""),
        AdaTools.NotYaz => WriteNoteAsync(Text(args, "metin")),
        AdaTools.NotBitir => EndNoteAsync(),
        _ => Done("hata: böyle bir işlev yok"),
    };

    private static Task<string> Done(string result) => Task.FromResult(result);

    // --- Ses ---

    private string SetVolume(int value, bool relative)
    {
        if (ctx.Audio is not { HasOutput: true } audio)
        {
            return Unavailable("ses");
        }

        var current = (int)Math.Round(audio.Volume * 100);
        var level = Math.Clamp(relative ? current + value : value, 0, 100);
        audio.SetVolume(level / 100f);
        if (audio.Muted && level > 0)
        {
            audio.SetMuted(false);
        }

        return $"tamam: ses %{level}";
    }

    private string Mute(bool muted)
    {
        if (ctx.Audio is not { HasOutput: true } audio)
        {
            return Unavailable("ses");
        }

        audio.SetMuted(muted);
        return muted ? "tamam: ses kapatıldı" : "tamam: ses açıldı";
    }

    private string PickDevice(string spoken, bool output)
    {
        if (ctx.Audio is not { } audio)
        {
            return Unavailable(output ? "ses çıkışı" : "ses girişi");
        }

        var devices = output ? audio.Outputs() : audio.Inputs();
        if (devices.Count == 0)
        {
            return Unavailable(output ? "ses çıkışı" : "ses girişi");
        }

        var names = devices.Select(d => d.Name).ToList();
        switch (NameMatch.Best(spoken, names))
        {
            case NameMatchResult.Found found:
                var device = devices[found.Index];
                var kind = output ? "çıkış" : "giriş";
                if (device.IsDefault)
                {
                    return $"tamam: {kind} cihazı zaten {device.Name}";
                }

                if (output)
                {
                    audio.SetDefaultOutput(device.Id);
                }
                else
                {
                    audio.SetDefaultInput(device.Id);
                }

                return $"tamam: {kind} cihazı {device.Name}";
            case NameMatchResult.Ambiguous ambiguous:
                return $"hata: birden fazla cihaz uyuyor: {string.Join(", ", ambiguous.Indexes.Select(i => names[i]))}; hangisi?";
            default:
                return $"hata: '{spoken}' adlı cihaz yok; olanlar: {string.Join(", ", names)}";
        }
    }

    private string Microphone(bool on)
    {
        if (ctx.Audio is not { } audio || audio.Mic == Core.Audio.MicState.Missing)
        {
            return Unavailable("mikrofon");
        }

        var isOn = audio.Mic == Core.Audio.MicState.On;
        if (isOn != on)
        {
            audio.ToggleMic();
        }

        if (on)
        {
            return "tamam: mikrofon açıldı";
        }

        ctx.EndSessionAfterSpeech();
        return "tamam: mikrofon kapatıldı; konuşma bitiyor";
    }

    // --- Ekran ve telsizler ---

    private async Task<string> BrightnessAsync(int value, bool relative)
    {
        if (ctx.Brightness is not { } brightness)
        {
            return Unavailable("parlaklık");
        }

        await brightness.RefreshAsync();
        var displays = brightness.Displays;
        var target = displays.FirstOrDefault(d => d.IsInternal) ?? (displays.Count == 1 ? displays[0] : null);
        if (target is null)
        {
            return Unavailable("parlaklık");
        }

        var level = Math.Clamp(relative ? target.Level + value : value, 0, 100);
        brightness.Set(target.Id, level);
        return $"tamam: parlaklık %{level}";
    }

    private async Task<string> RadioAsync(RadioKind kind, string label, bool on)
    {
        var radios = ctx.Radios;
        var present = kind == RadioKind.WiFi ? radios?.HasWifi == true : radios?.HasBluetooth == true;
        if (radios is null || !present)
        {
            return Unavailable(label);
        }

        var isOn = kind == RadioKind.WiFi ? radios.WifiOn : radios.BluetoothOn;
        if (isOn == on)
        {
            return $"tamam: {label} zaten {(on ? "açık" : "kapalı")}";
        }

        if (kind == RadioKind.WiFi && !on)
        {
            // İnternet kesilince Ada'nın yanıtı da kesilir: önce konuşsun, sonra kapansın.
            ctx.EndSessionAfterSpeech();
            _ = DeferAsync(() => radios.SetAsync(kind, false));
            return $"tamam: Wi-Fi {DisruptiveDelay.TotalSeconds:F0} sn sonra kapanacak; internet kesileceği için konuşma bitiyor";
        }

        return await radios.SetAsync(kind, on)
            ? $"tamam: {label} {(on ? "açıldı" : "kapatıldı")}"
            : $"hata: {label} değiştirilemedi";
    }

    // --- Medya ---

    private async Task<string> MediaAsync(string command)
    {
        if (ctx.Media is not { } media)
        {
            return Unavailable("medya kontrolü");
        }

        if (media.Current is not { } now)
        {
            return "hata: şu an çalan ya da duraklatılmış bir medya yok";
        }

        var playing = now.State == PlaybackState.Playing;
        switch (command.Trim().ToLower(Turkish))
        {
            case "oynat" or "devam" or "çal":
                if (playing)
                {
                    return "tamam: zaten çalıyor";
                }

                await media.TogglePlayPauseAsync();
                return "tamam: oynatıldı";
            case "duraklat" or "durdur":
                if (!playing)
                {
                    return "tamam: zaten duraklatılmış";
                }

                await media.TogglePlayPauseAsync();
                return "tamam: duraklatıldı";
            case "sonraki" or "ileri":
                await media.NextAsync();
                return "tamam: sonraki parçaya geçildi";
            case "onceki" or "önceki" or "geri":
                await media.PreviousAsync();
                return "tamam: önceki parçaya geçildi";
            default:
                return "hata: geçersiz değer: komut";
        }
    }

    private async Task<string> SpotifyAsync(string query)
    {
        if (ctx.Spotify is not { IsConfigured: true } spotify)
        {
            return "hata: Spotify bağlı değil";
        }

        var (outcome, title) = await spotify.PlayAsync(query, () =>
        {
            ctx.Launcher.Open("spotify:");
            return Task.CompletedTask;
        });
        return outcome switch
        {
            Spotify.SpotifyPlayOutcome.Playing => $"tamam: {title} çalıyor",
            Spotify.SpotifyPlayOutcome.NeedsReconnect => "hata: Spotify izninin yenilenmesi gerekiyor; müzik panelinden 'İzni yenile'",
            Spotify.SpotifyPlayOutcome.NotConnected => "hata: Spotify bağlı değil",
            Spotify.SpotifyPlayOutcome.NoDevice => "hata: Spotify açılamadı",
            Spotify.SpotifyPlayOutcome.PremiumRequired => "hata: Spotify Premium gerekiyor",
            Spotify.SpotifyPlayOutcome.NotFound => $"hata: Spotify'da '{query}' bulunamadı",
            _ => "hata: Spotify çalamadı",
        };
    }

    // --- Uygulamalar ---

    private async Task<string> OpenAppAsync(string spoken)
    {
        var items = (await ctx.Apps.GetItemsAsync()).Where(i => i.Kind == SearchKind.App).ToList();
        bool Has(string name) => items.Any(i => string.Equals(i.Name, name, StringComparison.CurrentCultureIgnoreCase));
        foreach (var item in items.ToList())
        {
            foreach (var alias in AppAliases.For(item.Name))
            {
                if (!Has(alias))
                {
                    items.Add(item with { Name = alias });
                }
            }
        }

        foreach (var system in SystemApps)
        {
            if (!Has(system.Name))
            {
                items.Add(system);
            }
        }

        var names = items.Select(i => i.Name).ToList();
        switch (NameMatch.Best(spoken, names))
        {
            case NameMatchResult.Found found:
                var item = items[found.Index];
                return ItemLauncher.Open(item) ? $"tamam: {item.Name} açılıyor" : $"hata: {item.Name} açılamadı";
            case NameMatchResult.Ambiguous ambiguous:
                return $"hata: birden fazla uygulama uyuyor: {string.Join(", ", ambiguous.Indexes.Take(5).Select(i => names[i]))}; hangisi?";
            default:
                return $"hata: '{spoken}' adlı uygulama bulunamadı; Başlat'taki adı farklıysa (ör. İngilizcesi) o adla bir kez daha dene";
        }
    }

    /// <summary>
    /// Uygulama adı, dosya adı ya da Türkçe takma adıyla eşler ve o uygulamanın tüm pencerelerine kapatma isteği gönderir.
    /// Asla zorla öldürmez (uygulama "kaydet?" diye sorabilir).
    /// </summary>
    private string CloseApp(string spoken)
    {
        var windows = ctx.OpenWindows();
        var candidates = new List<(string Name, string Group)>();
        foreach (var w in windows)
        {
            var group = GroupOf(w);
            if (w.AppName.Length > 0)
            {
                candidates.Add((w.AppName, group));
            }

            if (w.ExeName.Length > 0)
            {
                candidates.Add((w.ExeName, group));
            }

            foreach (var alias in AppAliases.For(w.AppName).Concat(AppAliases.For(w.ExeName)))
            {
                candidates.Add((alias, group));
            }
        }

        var names = candidates.Select(c => c.Name).ToList();
        string? chosen = null;
        switch (NameMatch.Best(spoken, names))
        {
            case NameMatchResult.Found found:
                chosen = candidates[found.Index].Group;
                break;
            case NameMatchResult.Ambiguous ambiguous:
                var groups = ambiguous.Indexes.Select(i => candidates[i].Group).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
                if (groups.Count > 1)
                {
                    return $"hata: birden fazla uygulama uyuyor: {string.Join(", ", groups.Take(5))}; hangisi?";
                }

                chosen = groups[0];
                break;
        }

        if (chosen is not null)
        {
            var targets = windows.Where(w => string.Equals(GroupOf(w), chosen, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var w in targets)
            {
                Native.RequestClose(w.Hwnd);
            }

            return $"tamam: {chosen} için {targets.Count} pencereye kapatma isteği gönderildi";
        }

        // Pencere başlığıyla eşlenmez: "Discord'u kapat" derken başlığında Discord geçen bütün bir tarayıcı penceresi
        // (sekmeleriyle) kapanmasın.
        return $"hata: açık '{spoken}' uygulaması yok";
    }

    private static string GroupOf(OpenWindow w) => w.AppName.Length > 0 ? w.AppName : w.ExeName;

    // --- Tarayıcı ve güncel bilgi ---

    private string OpenSite(string address)
    {
        if (ToolArgs.NormalizeUrl(address) is not { } url)
        {
            return "hata: geçersiz adres";
        }

        ctx.Launcher.Open(url);
        return $"tamam: {url} açıldı";
    }

    private string GoogleSearch(string query)
    {
        ctx.Launcher.Open(ToolArgs.GoogleSearchUrl(query));
        return $"tamam: Google'da '{query.Trim()}' arandı";
    }

    private async Task<string> LookupAsync(string question)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        var result = await ctx.Info.AskAsync(question, timeout.Token);
        ctx.LookupUsed(result);
        if (!result.Ok)
        {
            return "hata: güncel bilgiye ulaşılamadı";
        }

        return result.Searched ? "tamam: " + result.Text : "tamam: " + result.Text + " [not: Google'da aranmadı, eski olabilir]";
    }

    private string WhatsApp(string person, string message)
    {
        switch (WhatsAppLink.Build(ctx.Settings().WhatsAppContacts, person, message))
        {
            case WhatsAppLinkResult.Link link:
                ctx.Launcher.Open(link.Uri);
                return $"tamam: {link.ContactName} sohbeti mesaj hazır açıldı; göndermek kullanıcıda";
            case WhatsAppLinkResult.Ambiguous ambiguous:
                return $"hata: birden fazla kişi uyuyor: {string.Join(", ", ambiguous.Names)}; hangisi?";
            default:
                return $"hata: '{person}' rehberde yok; kişiler ayar dosyasında (Assistant.WhatsAppContacts)";
        }
    }

    // --- Windows ---

    private string Screenshot()
    {
        ctx.Launcher.Screenshot();
        return "tamam: ekran alıntısı aracı açıldı";
    }

    private string Lock()
    {
        ctx.EndSessionAfterSpeech();
        ctx.Launcher.Lock();
        return "tamam: bilgisayar kilitlendi; konuşma bitiyor";
    }

    private string Sleep()
    {
        ctx.EndSessionAfterSpeech();
        _ = DeferAsync(() =>
        {
            ctx.Launcher.Sleep();
            return Task.CompletedTask;
        });
        return $"tamam: bilgisayar {DisruptiveDelay.TotalSeconds:F0} sn sonra uykuya geçecek; konuşma bitiyor";
    }

    private string Power(bool restart)
    {
        switch ((restart ? _rebootGate : _shutdownGate).Press())
        {
            case ConfirmStep.Ask:
                return "onay gerekiyor: kullanıcıya 'emin misin?' diye sor; onaylarsa 30 sn içinde yeniden çağır";
            case ConfirmStep.WaitForUser:
                return "onay gerekiyor: kullanıcı henüz yanıt vermedi; önce sor ve yanıtını bekle, yalnız 'evet' derse yeniden çağır";
        }

        ctx.EndSessionAfterSpeech();
        if (restart)
        {
            ctx.Power.Reboot();
            return "tamam: bilgisayar yeniden başlatılıyor";
        }

        ctx.Power.Shutdown();
        return "tamam: bilgisayar kapatılıyor";
    }

    private string EndConversation()
    {
        ctx.EndSessionAfterSpeech();
        return "tamam";
    }

    // --- Hafıza ---

    private string Remember(string topic, string fact) => ctx.Memory.Remember(topic, fact) switch
    {
        MemoryChange.Saved { Evicted: { } evicted } => $"tamam: kaydedildi; yer açmak için en eski '{evicted.Topic}' maddesi silindi",
        MemoryChange.Saved => "tamam: kaydedildi",
        MemoryChange.Updated => "tamam: güncellendi",
        MemoryChange.Rejected rejected => "hata: kaydedilmedi: " + rejected.Reason,
        _ => "hata: kaydedilmedi",
    };

    private string Forget(string topic) => ctx.Memory.Forget(topic) switch
    {
        MemoryForget.Removed => "tamam: silindi",
        MemoryForget.WriteFailed => "hata: silinemedi: " + MemoryFile.WriteFailed,
        _ => $"hata: '{topic}' adlı kayıt yok",
    };

    // --- Not ---

    /// <summary>
    /// Ada'nın not dosyasını Not Defteri'nde açar; not aynı cümlede söylendiyse yazıp hemen bitirir. Açılış sürerken gelen komutlar
    /// da yürütülmez; oturum bu arada değiştiyse (kapatılıp yeniden açıldı) not kipi başlamaz.
    /// </summary>
    private async Task<string> TakeNoteAsync(string first)
    {
        var generation = Notes.Generation;
        Notes.BeginOpening();
        var target = await NotepadWriter.OpenNewAsync(NotepadWriter.NotesFolder, DateTime.Now);
        if (target is null)
        {
            Notes.CancelOpening();
            return first.Length > 0 ? ToClipboard(first, "not dosyası oluşturulamadı") : "hata: not dosyası oluşturulamadı";
        }

        if (!Notes.TryStart(generation))
        {
            return "hata: oturum kapandı";
        }

        _note = target;
        if (first.Length > 0)
        {
            var written = await WriteNoteAsync(first);
            FinishNote();
            return written.StartsWith("hata", StringComparison.Ordinal) ? written
                : target.Document is null ? $"tamam: not Belgeler\\Ada Notları\\{target.FileName} dosyasına yazıldı (Not Defteri görünmedi); bunu kısaca söyle"
                : "tamam: not yazıldı; yalnız 'Not aldım' de";
        }

        return target.Document is null
            ? $"tamam: Not Defteri görünmedi ama not Belgeler\\Ada Notları\\{target.FileName} dosyasına yazılacak; bunu kısaca söyle ve bekle"
            : "tamam: not defteri açık; kullanıcıya yalnız 'Dinliyorum' de ve bekle";
    }

    /// <summary>Parçayı nota ekler ve notun tamamını dosyaya ve Ada'nın sekmesine yazar (aynı anda tek yazma).</summary>
    private async Task<string> WriteNoteAsync(string text)
    {
        await _noteWrites.WaitAsync();
        try
        {
            var line = Notes.Append(text);
            if (line is null || _note is not { } target)
            {
                return "hata: önce not_al";
            }

            if (line.Length == 0)
            {
                return "tamam: yazıldı; hiçbir şey söyleme";
            }

            Notes.WriteStarted();
            try
            {
                return await NotepadWriter.WriteAsync(target, Notes.Text) == NoteWrite.Failed
                    ? ToClipboard(Notes.Text, "not dosyaya yazılamadı", finish: true)
                    : "tamam: yazıldı; hiçbir şey söyleme";
            }
            finally
            {
                Notes.WriteFinished();
            }
        }
        finally
        {
            _noteWrites.Release();
        }
    }

    /// <summary>Not yazılamadı: metin hiçbir pencereye gitmez, panoya konur (Windows satır sonlarıyla).</summary>
    private string ToClipboard(string text, string reason, bool finish = false)
    {
        if (finish)
        {
            Notes.Finish();
        }

        try
        {
            System.Windows.Clipboard.SetText(text.ReplaceLineEndings("\r\n"));
            return $"hata: {reason}; not panoya kopyalandı";
        }
        catch (System.Runtime.InteropServices.ExternalException)
        {
            return $"hata: {reason}; not panoya da kopyalanamadı";
        }
    }

    private Task<string> EndNoteAsync()
    {
        FinishNote();
        return Done("tamam: not bitti; yalnız 'Not aldım' de");
    }

    // --- Yardımcılar ---

    private static string Unavailable(string what) => $"hata: {what} bu bilgisayarda kullanılamıyor";

    private async Task DeferAsync(Func<Task> action)
    {
        await Task.Delay(DisruptiveDelay, time);
        await action();
    }

    private static int Int(JsonObject args, string key, int min, int max) => Unwrap(ToolArgs.Int(args, key, min, max));

    private static bool Bool(JsonObject args, string key) => Unwrap(ToolArgs.Bool(args, key));

    private static string Text(JsonObject args, string key) => Unwrap(ToolArgs.Text(args, key))!;

    private static T Unwrap<T>(ArgResult<T> result) => result.Error is { } error ? throw new ArgException(error) : result.Value!;

    /// <summary>Argüman hatası; metni modele "hata: …" olarak döner.</summary>
    private sealed class ArgException(string message) : Exception(message);
}
