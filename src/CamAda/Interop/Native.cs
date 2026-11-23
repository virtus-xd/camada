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

    /// <summary>Geçerli masaüstü duvar kağıdının dosya yolu; düz renk arka planda boş döner.</summary>
    public static unsafe string GetWallpaperPath()
    {
        const uint SPI_GETDESKWALLPAPER = 0x0073;
        var buffer = new char[1024];
        fixed (char* p = buffer)
        {
            return SystemParametersInfo(SPI_GETDESKWALLPAPER, (uint)buffer.Length, p, 0)
                ? new string(p).TrimEnd('\0')
                : string.Empty;
        }
    }

    /// <summary>Pencereyi köşeleri yuvarlatılmış bir dikdörtgenle sınırlar (piksel, pencereye göre).</summary>
    public static void SetRoundedRegion(nint hwnd, int left, int top, int right, int bottom, int cornerDiameter)
    {
        var region = CreateRoundRectRgn(left, top, right + 1, bottom + 1, cornerDiameter, cornerDiameter);
        SetWindowRgn(hwnd, region, true); // başarıda bölgenin sahipliği sisteme geçer
    }

    public static void ClearRegion(nint hwnd) => SetWindowRgn(hwnd, 0, true);

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static unsafe partial bool SystemParametersInfo(uint action, uint param, char* value, uint winIni);

    [LibraryImport("gdi32.dll")]
    private static partial nint CreateRoundRectRgn(int left, int top, int right, int bottom, int widthEllipse, int heightEllipse);

    [LibraryImport("user32.dll")]
    private static partial int SetWindowRgn(nint hwnd, nint region, [MarshalAs(UnmanagedType.Bool)] bool redraw);

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
