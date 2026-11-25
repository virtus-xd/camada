using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>ManagedShell'in sağlamadığı küçük Win32 çağrıları.</summary>
internal static partial class Native
{
    public const int GWL_EXSTYLE = -20;
    public const long WS_EX_NOACTIVATE = 0x08000000;
    public const int WM_WINDOWPOSCHANGING = 0x0046;
    public const uint SWP_NOMOVE = 0x0002;
    public const int WM_MOUSEACTIVATE = 0x0021;
    public const int MA_NOACTIVATE = 3;
    public const uint SWP_NOZORDER = 0x0004;
    public static readonly nint HWND_BOTTOM = 1;
    public const long WS_EX_TOOLWINDOW = 0x80;

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

    /// <summary>
    /// Tüm uygulamalara "görev çubuğu yeniden oluştu" der; tepsi simgelerini yeniden kaydederler. Ada tepsiyi barındırırken
    /// eklenen simgeler, ada beklenmedik kapanırsa Explorer'ın tepsisinde yoktur; bu yayın onları geri getirir.
    /// </summary>
    public static void BroadcastTaskbarCreated()
    {
        const nint HWND_BROADCAST = 0xFFFF;
        SendNotifyMessage(HWND_BROADCAST, (uint)TaskbarCreatedMessage, 0, 0);
    }

    /// <summary>Ekranı kilitler (Win+L ile aynı).</summary>
    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool LockWorkStation();

    [LibraryImport("user32.dll", EntryPoint = "SendNotifyMessageW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SendNotifyMessage(nint hwnd, uint message, nint wParam, nint lParam);

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

    /// <summary>Üst düzey pencereleri z-sırasında yukarıdan aşağı örnekler (koordinatlar fiziksel piksel).</summary>
    /// <param name="self">Sorgulayan pencere: örnekte IsSelf olarak işaretlenir (z-sırasında altındakiler onu örtemez).</param>
    public static List<Core.Island.WindowSample> SampleTopLevelWindows(int ownProcessId, nint self = 0)
    {
        const long WS_EX_TOOLWINDOW = 0x80, WS_EX_TRANSPARENT = 0x20;
        var samples = new List<Core.Island.WindowSample>(128);
        var className = new char[64];

        EnumWindows((hwnd, _) =>
        {
            GetWindowRect(hwnd, out var rect);
            DwmGetWindowAttribute(hwnd, DWMWA_CLOAKED, out var cloaked, sizeof(int));
            var exStyle = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
            GetWindowThreadProcessId(hwnd, out var pid);
            var classLength = GetClassName(hwnd, className, className.Length);
            var cls = new string(className, 0, Math.Max(classLength, 0));

            samples.Add(new Core.Island.WindowSample(
                new Core.Glass.RectD(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
                Visible: IsWindowVisible(hwnd),
                Cloaked: cloaked != 0,
                Ignorable: (exStyle & (WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE | WS_EX_TRANSPARENT)) != 0,
                HasTitle: GetWindowTextLength(hwnd) > 0,
                IsDesktop: cls is "Progman" or "WorkerW",
                IsOwn: pid == ownProcessId,
                IsSelf: self != 0 && hwnd == self));
            return true;
        }, 0);

        return samples;
    }

    /// <summary>
    /// Tam ekran adayı bir pencerenin anlık özeti. Etkin dikdörtgen ManagedShell'deki gibidir: başlık + kalın kenarlı
    /// ya da kenarlıklı açılır pencerede istemci alanı, diğerlerinde pencere dikdörtgeni (fiziksel piksel).
    /// </summary>
    public static Core.Island.FullScreenCandidate SampleFullScreenCandidate(nint hwnd, Core.Glass.RectD screenBounds,
        bool reportedByShell)
    {
        const long WS_CAPTION = 0x00C00000, WS_THICKFRAME = 0x00040000, WS_POPUP = 0x80000000, WS_BORDER = 0x00800000;
        var style = (long)GetWindowLongPtr(hwnd, GWL_STYLE) & 0xFFFFFFFF;

        RECT rect;
        if ((style & (WS_CAPTION | WS_THICKFRAME)) == (WS_CAPTION | WS_THICKFRAME)
            || (style & (WS_POPUP | WS_THICKFRAME)) == (WS_POPUP | WS_THICKFRAME)
            || (style & (WS_POPUP | WS_BORDER)) == (WS_POPUP | WS_BORDER))
        {
            GetClientRect(hwnd, out rect);
            MapWindowPoints(hwnd, 0, ref rect, 2);
        }
        else
        {
            GetWindowRect(hwnd, out rect);
        }

        return new Core.Island.FullScreenCandidate(
            new Core.Glass.RectD(rect.Left, rect.Top, rect.Right - rect.Left, rect.Bottom - rect.Top),
            screenBounds,
            Visible: IsWindowVisible(hwnd),
            Minimized: IsIconic(hwnd),
            Maximized: IsZoomed(hwnd),
            HasCaption: (style & WS_CAPTION) == WS_CAPTION,
            ReportedByShell: reportedByShell);
    }

    /// <summary>
    /// Pencerenin sahibini masaüstü penceresi (Progman) yapar. Sahipli pencere z-sırasında hep sahibinin üstünde kalır;
    /// Win+D masaüstünü pencerelerin önüne aldığında plak da onunla birlikte görünür kalır.
    /// </summary>
    public static void OwnByDesktop(nint hwnd)
    {
        const int GWLP_HWNDPARENT = -8;
        var desktop = GetShellWindow();
        if (desktop != 0)
        {
            SetWindowLongPtr(hwnd, GWLP_HWNDPARENT, desktop);
        }
    }

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();

    public static bool WindowExists(nint hwnd) => IsWindow(hwnd);

    /// <summary>
    /// Pencereye, öne getirmeden kapanma isteği gönderir (görev çubuğu küçük resmindeki × gibi). ManagedShell'in
    /// ApplicationWindow.Close'u önce SetForegroundWindow çağırır; bu, panelin odağını alıp onu kapatırdı. Uygulama
    /// "Kaydedilsin mi?" gibi bir soru gösterirse onu öne getirebilsin diye ön plana geçme izni verilir.
    /// </summary>
    public static void RequestClose(nint hwnd)
    {
        const uint WM_SYSCOMMAND = 0x0112;
        const nint SC_CLOSE = 0xF060;
        GetWindowThreadProcessId(hwnd, out var processId);
        AllowSetForegroundWindow(processId);
        PostMessage(hwnd, WM_SYSCOMMAND, SC_CLOSE, 0);
    }

    [DllImport("user32.dll")]
    private static extern bool IsWindow(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool AllowSetForegroundWindow(int processId);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(nint hwnd, uint msg, nint wParam, nint lParam);

    private const int GWL_STYLE = -16;

    [DllImport("user32.dll")]
    private static extern bool GetClientRect(nint hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern int MapWindowPoints(nint from, nint to, ref RECT points, int count);

    [DllImport("user32.dll")]
    private static extern bool IsIconic(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool IsZoomed(nint hwnd);

    public static (int X, int Y) GetCursorPosition()
    {
        GetCursorPos(out var point);
        return (point.X, point.Y);
    }

    private const int DWMWA_CLOAKED = 14;

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left, Top, Right, Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT
    {
        public int X, Y;
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    private static extern bool GetWindowRect(nint hwnd, out RECT rect);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLength(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(nint hwnd, char[] buffer, int maxCount);

    /// <summary>Pencere başlığı; yoksa boş.</summary>
    public static string TitleOf(nint hwnd)
    {
        var length = GetWindowTextLength(hwnd);
        if (length <= 0)
        {
            return "";
        }

        var buffer = new char[length + 1];
        var read = GetWindowText(hwnd, buffer, buffer.Length);
        return new string(buffer, 0, Math.Max(0, read));
    }

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetClassName(nint hwnd, char[] buffer, int maxCount);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out int processId);

    [DllImport("user32.dll")]
    private static extern bool GetCursorPos(out POINT point);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);

    public const long WS_EX_TRANSPARENT = 0x20;

    public static void AddExtendedStyle(nint hwnd, long style) => SetExtendedStyle(hwnd, style, on: true);

    public static void SetExtendedStyle(nint hwnd, long style, bool on)
    {
        var current = (long)GetWindowLongPtr(hwnd, GWL_EXSTYLE);
        var updated = on ? current | style : current & ~style;
        if (updated != current)
        {
            SetWindowLongPtr(hwnd, GWL_EXSTYLE, (nint)updated);
        }
    }

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial int RegisterWindowMessage(string message);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial nint GetWindowLongPtr(nint hwnd, int index);

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial nint SetWindowLongPtr(nint hwnd, int index, nint value);
}
