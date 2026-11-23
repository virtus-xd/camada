using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>ManagedShell'in sağlamadığı küçük Win32 çağrıları.</summary>
internal static partial class Native
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_NOACTIVATE = 0x08000000;
    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const uint SWP_NOMOVE = 0x0002;

    [StructLayout(LayoutKind.Sequential)]
    public struct WINDOWPOS
    {
        public nint hwnd;
        public nint hwndInsertAfter;
        public int x;
        public int y;
        public int cx;
        public int cy;
        public uint flags;
    }

    /// <summary>Explorer her başladığında tüm üst düzey pencerelere yayınlanan mesajın kimliği.</summary>
    public static readonly int TaskbarCreatedMessage = RegisterWindowMessage("TaskbarCreated");

    public static void AddExtendedStyle(nint hwnd, long style)
    {
        var current = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)(current | style));
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RegisterWindowMessage(string message);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
