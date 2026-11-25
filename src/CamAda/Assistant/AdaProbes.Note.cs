#if DEBUG
using System.IO;
using System.Windows.Automation;
using CamAda.Interop;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    private const ushort VkControl = 0x11;

    /// <summary>
    /// Not yazma, kullanıcının notlarına dokunmadan. Not Defteri'nin oturum dosyaları önce yedeklenir. Geçici klasörde kendi
    /// notunu (A) açar ve iki parça yazar; ekrandaki ve diskteki metin karşılaştırılır. Sonra aynı pencerede ikinci bir kendi
    /// notu (B) etkinken A'ya üçüncü parçayı yazar: B'ye hiçbir şey gitmemeli, A'nın dosyası üç satır olmalı. Kendi sekmelerini
    /// yalnız başlık denetimiyle Ctrl+W ile kapatır ve dosyaları siler. Yazma tuşla yapılmaz.
    /// </summary>
    private static async Task NoteProbeAsync()
    {
        Log($"ada-not-sina: yedek {BackupNotepadState() ?? "Not Defteri durumu yok"}; açık sekme {NotepadTabCount()}");
        var folder = Path.Combine(Path.GetTempPath(), "camada-not-deneme-" + Guid.NewGuid().ToString("N")[..8]);
        try
        {
            var a = await NotepadWriter.OpenNewAsync(folder, DateTime.Now);
            Log($"A açıldı={a is not null}, belge yakalandı={a?.Document is not null}, ad={a?.FileName}");
            if (a?.Document is null)
            {
                return;
            }

            var one = await NotepadWriter.WriteAsync(a, "Süt al.");
            var two = await NotepadWriter.WriteAsync(a, "Süt al.\nAnneni ara: ğüşıöç İ");
            await Task.Delay(400);
            Log($"A iki parça: {one}/{two}; ekranda \"{ReadDocument(NotepadWriter.LastOpened)?.ReplaceLineEndings("\\n")}\"; " +
                $"diskte \"{File.ReadAllText(a.Path).ReplaceLineEndings("\\n")}\"");

            var b = await NotepadWriter.OpenNewAsync(folder, DateTime.Now); // aynı dakika: "… (2).txt", aynı pencerede etkin
            await Task.Delay(400);
            var three = await NotepadWriter.WriteAsync(a, "Süt al.\nAnneni ara: ğüşıöç İ\nüçüncü");
            await Task.Delay(400);
            var inB = b is not null && Native.TitleOf(Native.GetForegroundWindow()).Contains(b.FileName, StringComparison.Ordinal)
                ? ReadDocument(Native.GetForegroundWindow())
                : null;
            Log($"B etkinken A'ya yazma: {three}; B'de {inB?.Length.ToString() ?? "okunamadı"} karakter; A diskte \"{File.ReadAllText(a.Path).ReplaceLineEndings("\\n")}\"");

            if (b is not null)
            {
                await CloseOwnNoteAsync(b.Path);
            }

            // A'yı yeniden etkinleştir (açık dosyayı yeniden açmak sekmesine geçer), ekranda üç satır olmalı, sonra kapat.
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("notepad.exe", $"\"{a.Path}\"") { UseShellExecute = true })?.Dispose();
            for (var i = 0; i < 30 && !Native.TitleOf(Native.GetForegroundWindow()).Contains(a.FileName, StringComparison.Ordinal); i++)
            {
                await Task.Delay(100);
            }

            Log($"A yeniden etkin; ekranda \"{ReadDocument(Native.GetForegroundWindow())?.ReplaceLineEndings("\\n")}\"");
            await CloseOwnNoteAsync(a.Path);
        }
        finally
        {
            Log($"bitti; açık sekme {NotepadTabCount()}");
            try
            {
                Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
            }
        }
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

    /// <summary>Çalışan Not Defteri pencerelerindeki toplam sekme sayısı (UI Automation).</summary>
    private static int NotepadTabCount()
    {
        var processes = System.Diagnostics.Process.GetProcessesByName("Notepad");
        try
        {
            return processes.SelectMany(p => Native.TopLevelWindowsOf(p.Id)).Sum(w => AutomationElement.FromHandle(w)
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
            var document = root.FindFirst(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            return document?.GetCurrentPattern(TextPattern.Pattern) is TextPattern text ? text.DocumentRange.GetText(-1) : null;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or ArgumentException)
        {
            return null;
        }
    }

    /// <summary>
    /// Denemenin kendi not sekmesini kapatır: ön pencere Not Defteri ve başlığında dosya adı varsa, sekme değiştirilmemişse
    /// (Ada dosyayı kendisi yazar, sekme "Unmodified" kalır) Ctrl+W. Sekme kapanmadıysa dosya silinmez.
    /// </summary>
    private static async Task CloseOwnNoteAsync(string path)
    {
        var name = Path.GetFileName(path);
        bool Ours() => NotepadWriter.IsNotepad(Native.GetForegroundWindow())
            && Native.TitleOf(Native.GetForegroundWindow()).Contains(name, StringComparison.Ordinal);
        if (!Ours())
        {
            Log($"not {name}: kendi sekmesi önde değil, dokunulmadı");
            return;
        }

        if (!Native.TitleOf(Native.GetForegroundWindow()).StartsWith('*'))
        {
            Native.SendChord(VkControl, 'W');
            await Task.Delay(800);
        }

        if (Ours())
        {
            Log($"not {name}: sekme kapanmadı, dosya bırakıldı");
            return;
        }

        File.Delete(path);
        Log($"not {name}: sekme kapandı, dosya silindi");
    }
}
#endif
