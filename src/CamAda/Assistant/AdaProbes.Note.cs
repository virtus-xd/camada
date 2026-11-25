#if DEBUG
using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using CamAda.Interop;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    private const ushort VkControl = 0x11;

    /// <summary>
    /// Not yazma, kullanıcının notlarına dokunmadan:
    /// (1) Denemenin kendi metin kutusuna Türkçe harfler ve satır sonuyla tuş gönderir.
    /// (2) Not Defteri önde değilken yazmanın reddedildiğine bakar.
    /// (3) Not Defteri'nin oturum dosyalarını yedekler, geçici klasörde kendi not dosyasını açar, başka adla yazmanın
    ///     reddedildiğine bakar, yazar, UI Automation ile geri okur, kaydeder, diskteki içeriği karşılaştırır. Yalnız kendi
    ///     sekmesini Ctrl+W ile kapatır ve dosyayı siler.
    /// Ctrl+A, Delete ve ESC göndermez; tıklamaz.
    /// </summary>
    private static async Task NoteProbeAsync()
    {
        // 1) Kendi penceremiz.
        var box = new TextBox { AcceptsReturn = true, FontSize = 16 };
        var window = new Window { Title = "CamAda not denemesi", Width = 420, Height = 160, Content = box, Topmost = true };
        window.Show();
        window.Activate();
        box.Focus();
        await Task.Delay(400);
        const string expected = "Çay ğüşıöç İ\nikinci satır";
        if (Native.GetForegroundWindow() == new System.Windows.Interop.WindowInteropHelper(window).Handle)
        {
            var sent = Native.TypeText(expected);
            await Task.Delay(600);
            var got = box.Text.Replace("\r\n", "\n");
            Log(got == expected ? $"yazma: tamam (gönderildi={sent})" : $"yazma: FARK: beklenen \"{expected}\", gelen \"{got}\"");
        }
        else
        {
            // Tuşlar yalnız kendi penceremize gider: öne gelmediysek bu adım atlanır (başka pencereye yazılmaz).
            Log("yazma: deneme penceresi öne gelmedi, bu adım atlandı");
        }

        // 2) Önde Not Defteri yokken yazma reddedilmeli.
        var before = box.Text;
        var refused = !await NotepadWriter.TryTypeAsync("Not 01.01.2000 00.00.txt", "x");
        await Task.Delay(300);
        Log($"Not Defteri değilken yazma reddedildi={refused}, kutu değişmedi={box.Text == before}");
        window.Close();
        await Task.Delay(1500); // deneme artık önde değil: Ada'nın arka planda açtığı duruma yakın

        // 3) Gerçek Not Defteri, kendi geçici dosyamız.
        var backup = BackupNotepadState();
        Log($"Not Defteri oturum yedeği: {backup ?? "Not Defteri durumu yok"}; açık sekme {NotepadTabCount()}");
        var folder = Path.Combine(Path.GetTempPath(), "camada-not-deneme-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var name = await NotepadWriter.OpenNewAsync(folder, DateTime.Now);
            var hwnd = NotepadWriter.LastOpened;
            Log($"açıldı={name is not null}, ad={name}, ön pencere başlığı=\"{Native.TitleOf(Native.GetForegroundWindow())}\"");
            if (name is null)
            {
                return;
            }

            Log($"başka adla yazma reddedildi={!await NotepadWriter.TryTypeAsync("baska.txt", "x")}");
            const string note = "CamAda deneme ğüş\nsatır 2";
            var typed = await NotepadWriter.TryTypeAsync(name, note);
            await Task.Delay(800);
            var read = ReadDocument(hwnd)?.Replace("\r\n", "\n").Replace("\r", "\n");
            Log($"yazıldı={typed}; geri okunan {(read == note ? "AYNI" : $"FARKLI: \"{read?.Replace("\n", "\\n")}\"")}");

            var saved = await NotepadWriter.TrySaveAsync(name);
            await Task.Delay(800);
            var onDisk = File.ReadAllText(Path.Combine(folder, name)).Replace("\r\n", "\n").Replace("\r", "\n");
            Log($"kaydedildi={saved}; diskte {(onDisk == note ? "AYNI" : $"FARKLI: \"{onDisk.Replace("\n", "\\n")}\"")}");

            if (Native.TitleOf(Native.GetForegroundWindow()).Contains(name, StringComparison.Ordinal))
            {
                Native.SendChord(VkControl, 'W'); // yalnız kendi (kaydedilmiş) sekmemiz
                await Task.Delay(1000);
            }

            Log($"kendi sekmesi kapandı={!NotepadWriter.IsNotepad(Native.GetForegroundWindow()) || !Native.TitleOf(Native.GetForegroundWindow()).Contains(name, StringComparison.Ordinal)}; açık sekme {NotepadTabCount()}");
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    /// <summary>
    /// Hız ölçümü (Not Defteri hızlı Unicode tuşlarını bozuyor): her biçim için ayrı geçici dosya açılır, metin parça parça
    /// yazılır, geri okunup karşılaştırılır, kaydedilip yalnız o sekme kapatılır.
    /// </summary>
    private static async Task NotePacingProbeAsync()
    {
        Log($"ada-not-hiz-sina: yedek {BackupNotepadState()}; açık sekme {NotepadTabCount()}");
        const string note = "CamAda deneme ğüşıöç İ, bir iki üç.\nsatır 2: süt ve ekmek al.";
        var folder = Path.Combine(Path.GetTempPath(), "camada-not-hiz-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            foreach (var (chunk, delay) in new[] { (1, 15), (2, 15), (4, 15), (8, 15), (1, 5) })
            {
                var name = await NotepadWriter.OpenNewAsync(Path.Combine(folder, $"{chunk}-{delay}"), DateTime.Now);
                if (name is null)
                {
                    Log($"parça {chunk}, {delay} ms: açılamadı");
                    continue;
                }

                var hwnd = NotepadWriter.LastOpened;
                await Task.Delay(300);
                var started = System.Diagnostics.Stopwatch.StartNew();
                for (var i = 0; i < note.Length && Native.GetForegroundWindow() == hwnd; i += chunk)
                {
                    Native.TypeText(note.Substring(i, Math.Min(chunk, note.Length - i)));
                    await Task.Delay(delay);
                }

                var took = started.ElapsedMilliseconds;
                await Task.Delay(700);
                var read = ReadDocument(hwnd)?.Replace("\r\n", "\n").Replace("\r", "\n");
                Log($"parça {chunk}, {delay} ms: {took} ms, {(read == note ? "AYNI" : $"FARKLI: \"{read?.Replace("\n", "\\n")}\"")}");
                if (await NotepadWriter.TrySaveAsync(name))
                {
                    await Task.Delay(500);
                    if (Native.TitleOf(Native.GetForegroundWindow()).Contains(name, StringComparison.Ordinal))
                    {
                        Native.SendChord(VkControl, 'W');
                        await Task.Delay(800);
                    }
                }
            }
        }
        finally
        {
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }

        Log($"bitti; açık sekme {NotepadTabCount()}");
    }

    /// <summary>Not Defteri'nin oturum dosyalarını (LocalState) %LOCALAPPDATA%\CamAda altına kopyalar.</summary>
    private static string? BackupNotepadState()
    {
        var source = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Packages", "Microsoft.WindowsNotepad_8wekyb3d8bbwe", "LocalState");
        if (!Directory.Exists(source))
        {
            return null;
        }

        var target = Path.Combine(LocalDir, $"notepad-yedek-{DateTime.Now:yyyyMMdd-HHmmss}");
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var copy = Path.Combine(target, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(copy)!);
            File.Copy(file, copy);
        }

        return target;
    }

    /// <summary>Çalışan Not Defteri'ndeki sekme sayısı (UI Automation); Not Defteri yoksa 0.</summary>
    private static int NotepadTabCount()
    {
        var processes = System.Diagnostics.Process.GetProcessesByName("Notepad");
        try
        {
            return processes.Where(p => p.MainWindowHandle != 0).Sum(p => AutomationElement.FromHandle(p.MainWindowHandle)
                .FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.TabItem)).Count);
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or ArgumentException)
        {
            return -1;
        }
        finally
        {
            foreach (var p in processes)
            {
                p.Dispose();
            }
        }
    }

    /// <summary>Not Defteri penceresinin etkin belgesinin metni (UI Automation); okunamazsa null.</summary>
    private static string? ReadDocument(nint hwnd)
    {
        try
        {
            var root = AutomationElement.FromHandle(hwnd);
            var document = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document))
                ?? root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit));
            return document?.GetCurrentPattern(TextPattern.Pattern) is TextPattern text ? text.DocumentRange.GetText(-1) : null;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }
}
#endif
