using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Automation;
using CamAda.Core.Assistant;
using CamAda.Interop;

namespace CamAda.Assistant;

/// <summary>Ada'nın açık not dosyası ve (bulunabildiyse) Not Defteri'ndeki sekmesinin belge öğesi.</summary>
internal sealed class NoteTarget(string path, AutomationElement? document)
{
    public string Path { get; } = path;

    public string FileName => System.IO.Path.GetFileName(Path);

    /// <summary>Ada'nın sekmesinin belge öğesi; Not Defteri 5 sn içinde göstermediyse null (not yine dosyaya yazılır).</summary>
    public AutomationElement? Document { get; } = document;
}

internal enum NoteWrite
{
    /// <summary>Dosyaya yazıldı ve Ada'nın sekmesinde görünüyor.</summary>
    Shown,

    /// <summary>Dosyaya yazıldı; sekme yok (kapatıldı ya da hiç görünmedi).</summary>
    FileOnly,

    /// <summary>Dosyaya yazılamadı.</summary>
    Failed,
}

/// <summary>
/// Ada'nın notu (tasarım "Ada ile not alma"). Not Defteri açılınca önceki oturumun sekmelerini geri yükler ve hangi sekmenin önde
/// olacağı belli değildir; Ada bu yüzden her not için kendi dosyasını açar. Tuş göndermez: açılışta, başlığı Ada'nın dosyası olan
/// pencerenin o anki (boş) belge öğesi yakalanır ve her parçada notun tamamı UI Automation ile o öğeye ve dosyaya doğrudan yazılır.
/// Öğe sekmeye bağlıdır (26.11 denemesi: başka sekme etkinken yazılan metin yine Ada'nın sekmesine gitti); Not Defteri'nin önde
/// olması gerekmez, pano kullanılmaz.
/// </summary>
internal static class NotepadWriter
{
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(5);

    public static string NotesFolder { get; } =
        System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Ada Notları");

    /// <summary>Son açılışta Ada'nın sekmesini gösteren Not Defteri penceresi (deneme geri okuma ve temizlik için).</summary>
    public static nint LastOpened { get; private set; }

    /// <summary>
    /// Klasörde boş not dosyası oluşturur ("Not 26.11.2026 01.07.txt", çakışırsa " (2)"…) ve Not Defteri'nde açar. En çok 5 sn
    /// içinde etkin sekmesi bu dosya olan Not Defteri penceresinin boş belge öğesini yakalar. Dosya oluşturulamazsa null;
    /// Not Defteri göstermezse belge öğesi olmadan döner (not dosyaya yazılır).
    /// </summary>
    public static async Task<NoteTarget?> OpenNewAsync(string folder, DateTime now)
    {
        string path;
        try
        {
            Directory.CreateDirectory(folder);
            path = UniquePath(folder, NoteSession.FileNameFor(now));
            await using (File.Create(path))
            {
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }

        try
        {
            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException)
        {
            return new NoteTarget(path, null);
        }

        var name = System.IO.Path.GetFileName(path);
        var started = Stopwatch.StartNew();
        while (started.Elapsed < OpenTimeout)
        {
            await Task.Delay(100);
            if (FindWindowTitled(name) is var window and not 0 && await Task.Run(() => EmptyDocumentOf(window)) is { } document
                && Native.TitleOf(window).Contains(name, StringComparison.Ordinal))
            {
                LastOpened = window;
                return new NoteTarget(path, document);
            }
        }

        return new NoteTarget(path, null);
    }

    /// <summary>Notun tamamını dosyaya yazar ve Ada'nın sekmesinde gösterir (satır sonları Windows biçiminde).</summary>
    public static async Task<NoteWrite> WriteAsync(NoteTarget target, string fullText)
    {
        var text = fullText.ReplaceLineEndings("\r\n");
        try
        {
            await File.WriteAllTextAsync(target.Path, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return NoteWrite.Failed;
        }

        if (target.Document is not { } document)
        {
            return NoteWrite.FileOnly;
        }

        try
        {
            await Task.Run(() => ((ValuePattern)document.GetCurrentPattern(ValuePattern.Pattern)).SetValue(text));
            return NoteWrite.Shown;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or COMException)
        {
            return NoteWrite.FileOnly; // sekme kapatıldı: not dosyada
        }
    }

    public static bool IsNotepad(nint hwnd)
    {
        if (hwnd == 0)
        {
            return false;
        }

        try
        {
            using var process = Process.GetProcessById(Native.ProcessIdOf(hwnd));
            return string.Equals(process.ProcessName, "Notepad", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return false;
        }
    }

    /// <summary>Pencerenin etkin belge öğesi, yalnız boşsa (yeni not dosyası boştur: başka bir sekme yakalanmasın).</summary>
    private static AutomationElement? EmptyDocumentOf(nint window)
    {
        try
        {
            var document = AutomationElement.FromHandle(window).FindFirst(TreeScope.Descendants,
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Document));
            return document?.GetCurrentPattern(TextPattern.Pattern) is TextPattern text && text.DocumentRange.GetText(1).Length == 0
                && document.TryGetCurrentPattern(ValuePattern.Pattern, out _)
                ? document
                : null;
        }
        catch (Exception ex) when (ex is ElementNotAvailableException or InvalidOperationException or ArgumentException or COMException)
        {
            return null;
        }
    }

    private static string UniquePath(string folder, string fileName)
    {
        var path = System.IO.Path.Combine(folder, fileName);
        var stem = System.IO.Path.GetFileNameWithoutExtension(fileName);
        for (var i = 2; File.Exists(path); i++)
        {
            path = System.IO.Path.Combine(folder, $"{stem} ({i}).txt");
        }

        return path;
    }

    /// <summary>Etkin sekmesi bu dosya olan Not Defteri penceresi (başlıkta dosya adı); yoksa 0.</summary>
    private static nint FindWindowTitled(string fileName)
    {
        var processes = Process.GetProcessesByName("Notepad");
        try
        {
            foreach (var process in processes)
            {
                foreach (var window in Native.TopLevelWindowsOf(process.Id))
                {
                    if (Native.TitleOf(window).Contains(fileName, StringComparison.Ordinal))
                    {
                        return window;
                    }
                }
            }

            return 0;
        }
        finally
        {
            foreach (var p in processes)
            {
                p.Dispose();
            }
        }
    }
}
