using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>Çift tıklama süresi (tepsi paneli için) ve genel kısayollar.</summary>
internal static partial class Native
{
    public const int WM_HOTKEY = 0x0312;
    public const uint MOD_NOREPEAT = 0x4000;

    /// <summary>Sistemin çift tıklama süresi (ms); tepsi simgelerine iletilir.</summary>
    public static int DoubleClickTime => (int)GetDoubleClickTime();

    [LibraryImport("user32.dll")]
    private static partial uint GetDoubleClickTime();

    [LibraryImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool RegisterHotKey(IntPtr hWnd, int id, uint modifiers, uint virtualKey);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool UnregisterHotKey(IntPtr hWnd, int id);
}
