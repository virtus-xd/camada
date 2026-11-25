using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using CamAda.Core.Assistant;
using CamAda.Interop;

namespace CamAda.Assistant;

/// <summary>
/// Ada'nın notu için Not Defteri (tasarım "Ada ile not alma"). Not Defteri açılınca önceki oturumun sekmelerini geri yükler ve
/// hangi sekmenin önde olacağı belli değildir; bu yüzden Ada her not için kendi dosyasını açar ve tuşları (yazı ve Ctrl+S) yalnız
/// başlığında o dosyanın adı olan, yönetici olmayan ön Not Defteri penceresine gönderir. Pano kullanılmaz.
/// </summary>
internal static class NotepadWriter
{
    private const ushort VkControl = 0x11;
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan ForegroundNudgeAfter = TimeSpan.FromSeconds(1.5);
    private static readonly TimeSpan ModifierWait = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan CharacterPause = TimeSpan.FromMilliseconds(5); // zamanlayıcıyla ~15 ms

    public static string NotesFolder { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Ada Notları");

    /// <summary>Son açılışta öne gelen Not Defteri penceresi (deneme geri okuma ve temizlik için).</summary>
    public static nint LastOpened { get; private set; }

    /// <summary>
    /// Klasörde boş not dosyası oluşturur ("Not 26.11.2026 01.07.txt", çakışırsa " (2)"…) ve Not Defteri'nde açar. En çok
    /// 5 sn içinde başlığında dosya adı olan Not Defteri penceresi öne gelince dosya adını döner; gelmezse null.
    /// </summary>
    public static async Task<string?> OpenNewAsync(string folder, DateTime now)
    {
        string path;
        try
        {
            Directory.CreateDirectory(folder);
            path = UniquePath(folder, NoteSession.FileNameFor(now));
            await using (File.Create(path))
            {
            }

            Process.Start(new ProcessStartInfo("notepad.exe", $"\"{path}\"") { UseShellExecute = true })?.Dispose();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
        {
            return null;
        }

        var name = Path.GetFileName(path);
        var started = Stopwatch.StartNew();
        var nudged = false;
        while (started.Elapsed < OpenTimeout)
        {
            await Task.Delay(100);
            var front = Native.GetForegroundWindow();
            if (IsOurs(front, name))
            {
                LastOpened = front;
                return name;
            }

            if (!nudged && started.Elapsed >= ForegroundNudgeAfter && FindWindowTitled(name) is var window and not 0)
            {
                // Windows arka plandaki sürecin pencere öne almasını engelleyebilir: anlamsız bir tuş bu hakkı verir.
                nudged = true;
                Native.SendMaskKey();
                Native.SetForegroundWindow(window);
            }
        }

        return null;
    }

    /// <summary>
    /// Ön pencere Ada'nın not dosyasıysa metni yazar; değilse hiçbir pencereye tuş gitmez ve false. Not Defteri art arda gelen
    /// Unicode tuşlarını bozuyor (26.11 ölçümü: 2+ karakterlik parçalar bozuk, tek karakter + ~15 ms doğru): karakter karakter
    /// yazılır ve her karakterden önce pencerenin hâlâ önde olduğuna bakılır; araya başka pencere girerse yazma durur.
    /// </summary>
    public static async Task<bool> TryTypeAsync(string fileName, string text)
    {
        if (await ReadyAsync(fileName) is not { } front)
        {
            return false;
        }

        for (var i = 0; i < text.Length; i++)
        {
            if (Native.GetForegroundWindow() != front)
            {
                return false;
            }

            var length = char.IsHighSurrogate(text[i]) && i + 1 < text.Length ? 2 : 1;
            if (!Native.TypeText(text.Substring(i, length)))
            {
                return false;
            }

            i += length - 1;
            await Task.Delay(CharacterPause);
        }

        return true;
    }

    /// <summary>Ön pencere Ada'nın not dosyasıysa Ctrl+S ile kaydeder.</summary>
    public static async Task<bool> TrySaveAsync(string fileName) =>
        await ReadyAsync(fileName) is { } front && Native.GetForegroundWindow() == front && Native.SendChord(VkControl, 'S');

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

    private static bool IsOurs(nint hwnd, string fileName) =>
        IsNotepad(hwnd) && Native.TitleOf(hwnd).Contains(fileName, StringComparison.Ordinal);

    /// <summary>
    /// Ön pencere başlığında dosya adı olan, yönetici olmayan Not Defteri ise ve değiştirici tuşlar (ör. hâlâ basılı Win+Alt+A)
    /// en çok 1 sn içinde bırakılırsa o pencere; değilse null.
    /// </summary>
    private static async Task<nint?> ReadyAsync(string fileName)
    {
        var front = Native.GetForegroundWindow();
        if (!IsOurs(front, fileName) || Native.IsElevated(Native.ProcessIdOf(front)))
        {
            return null;
        }

        var waited = Stopwatch.StartNew();
        while (Native.AnyModifierDown())
        {
            if (waited.Elapsed >= ModifierWait)
            {
                return null;
            }

            await Task.Delay(50);
        }

        return IsOurs(Native.GetForegroundWindow(), fileName) ? front : null;
    }

    private static string UniquePath(string folder, string fileName)
    {
        var path = Path.Combine(folder, fileName);
        var stem = Path.GetFileNameWithoutExtension(fileName);
        for (var i = 2; File.Exists(path); i++)
        {
            path = Path.Combine(folder, $"{stem} ({i}).txt");
        }

        return path;
    }

    private static nint FindWindowTitled(string fileName)
    {
        var processes = Process.GetProcessesByName("Notepad");
        try
        {
            return processes.Select(p => p.MainWindowHandle).FirstOrDefault(h => h != 0 && Native.TitleOf(h).Contains(fileName, StringComparison.Ordinal));
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
