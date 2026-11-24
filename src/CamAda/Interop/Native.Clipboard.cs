using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>Pano dinleyicisi ve panonun doğrudan okunması (Plan 4.2).</summary>
internal static partial class Native
{
    public const int WM_CLIPBOARDUPDATE = 0x031D;
    public const uint CF_UNICODETEXT = 13;
    public const uint CF_HDROP = 15;
    public const uint CF_DIB = 8;

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AddClipboardFormatListener(nint hwnd);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RemoveClipboardFormatListener(nint hwnd);

    [LibraryImport("user32.dll")]
    public static partial uint GetClipboardSequenceNumber();

    [LibraryImport("user32.dll", EntryPoint = "RegisterClipboardFormatW", StringMarshalling = StringMarshalling.Utf16)]
    public static partial uint RegisterClipboardFormat(string name);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool IsClipboardFormatAvailable(uint format);

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool OpenClipboard(nint owner);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseClipboard();

    [LibraryImport("user32.dll")]
    public static partial nint GetClipboardData(uint format);

    [LibraryImport("user32.dll")]
    public static partial nint GetClipboardOwner();

    [LibraryImport("kernel32.dll")]
    public static partial nint GlobalLock(nint handle);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GlobalUnlock(nint handle);

    [LibraryImport("kernel32.dll")]
    public static partial nuint GlobalSize(nint handle);

    [LibraryImport("user32.dll", EntryPoint = "SendMessageTimeoutW")]
    private static partial nint SendMessageTimeout(nint hwnd, uint msg, nint wParam, nint lParam, uint flags, uint timeoutMs, out nint result);

    /// <summary>
    /// Pencerenin iş parçacığı <paramref name="timeoutMs"/> içinde mesaj işliyor mu (WM_NULL). Pano sahibi meşgulken
    /// panoyu açıp "sonra hazırlanacak" veriyi istemek iki tarafı da kilitler ve sahibin kopyasını bozar.
    /// </summary>
    public static bool IsResponsive(nint hwnd, uint timeoutMs) =>
        SendMessageTimeout(hwnd, 0 /* WM_NULL */, 0, 0, 0x0002 /* SMTO_ABORTIFHUNG */, timeoutMs, out _) != 0;

    /// <summary>Açık panodaki bir biçimin baytları; yoksa ya da <paramref name="maxBytes"/>'ı aşıyorsa null.</summary>
    public static byte[]? ReadClipboardBytes(uint format, long maxBytes, out bool tooBig)
    {
        tooBig = false;
        var handle = GetClipboardData(format);
        if (handle == 0)
        {
            return null;
        }

        var size = (long)GlobalSize(handle);
        if (size > maxBytes)
        {
            tooBig = true;
            return null;
        }

        var pointer = GlobalLock(handle);
        if (pointer == 0)
        {
            return null;
        }

        try
        {
            var bytes = new byte[size];
            Marshal.Copy(pointer, bytes, 0, (int)size);
            return bytes;
        }
        finally
        {
            GlobalUnlock(handle);
        }
    }

    /// <summary>Pano sahibi pencerenin süreç adı (uzantısız); bilinmiyorsa null.</summary>
    public static string? ClipboardOwnerProcessName()
    {
        var owner = GetClipboardOwner();
        if (owner == 0)
        {
            return null;
        }

        GetWindowThreadProcessId(owner, out var processId);
        try
        {
            using var process = System.Diagnostics.Process.GetProcessById(processId);
            return process.ProcessName;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            return null;
        }
    }
}
