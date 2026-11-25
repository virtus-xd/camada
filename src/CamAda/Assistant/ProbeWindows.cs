#if DEBUG
using System.Runtime.InteropServices;
using System.Text;
using CamAda.Core.Search;

namespace CamAda.Assistant;

/// <summary>
/// Yalnız Debug denemesi için açık pencere listesi. Uygulamada ManagedShell'in görev listesi kullanılır; deneme ayrı
/// süreçte çalıştığından (ve ManagedShell'in görev servisi görev çubuğu rolünü üstlendiğinden) burada EnumWindows ile
/// görünür, sahipsiz, başlıklı ve gizlenmemiş üst düzey pencereler okunur.
/// </summary>
internal static partial class ProbeWindows
{
    public static IReadOnlyList<OpenWindow> List(IReadOnlyList<SearchItem> apps)
    {
        var result = new List<OpenWindow>();
        EnumWindows((hwnd, _) =>
        {
            if (!IsWindowVisible(hwnd) || GetWindow(hwnd, 4 /* GW_OWNER */) != 0)
            {
                return true;
            }

            var exStyle = GetWindowLongPtrW(hwnd, -20 /* GWL_EXSTYLE */);
            if ((exStyle & 0x80 /* WS_EX_TOOLWINDOW */) != 0
                || (DwmGetWindowAttribute(hwnd, 14 /* DWMWA_CLOAKED */, out var cloaked, sizeof(int)) == 0 && cloaked != 0))
            {
                return true;
            }

            var length = GetWindowTextLengthW(hwnd);
            if (length == 0)
            {
                return true;
            }

            var title = new StringBuilder(length + 1);
            _ = GetWindowTextW(hwnd, title, title.Capacity);
            var (exe, aumid) = ProcessInfo(hwnd);
            result.Add(new OpenWindow(hwnd, OpenWindowNames.AppName(aumid, exe, apps),
                System.IO.Path.GetFileNameWithoutExtension(exe ?? ""), title.ToString()));
            return true;
        }, 0);
        return result;
    }

    private static (string? Exe, string? Aumid) ProcessInfo(nint hwnd)
    {
        _ = GetWindowThreadProcessId(hwnd, out var pid);
        var process = OpenProcess(0x1000 /* PROCESS_QUERY_LIMITED_INFORMATION */, false, pid);
        if (process == 0)
        {
            return (null, null);
        }

        try
        {
            var path = new StringBuilder(1024);
            var size = (uint)path.Capacity;
            var exe = QueryFullProcessImageNameW(process, 0, path, ref size) ? path.ToString() : null;
            var aumid = new StringBuilder(256);
            var aumidLength = (uint)aumid.Capacity;
            var id = GetApplicationUserModelId(process, ref aumidLength, aumid) == 0 ? aumid.ToString() : null;
            return (exe, id);
        }
        finally
        {
            CloseHandle(process);
        }
    }

    private delegate bool EnumWindowsProc(nint hwnd, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc callback, nint lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(nint hwnd);

    [DllImport("user32.dll")]
    private static extern nint GetWindow(nint hwnd, uint cmd);

    [DllImport("user32.dll")]
    private static extern nint GetWindowLongPtrW(nint hwnd, int index);

    [DllImport("user32.dll")]
    private static extern int GetWindowTextLengthW(nint hwnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextW(nint hwnd, StringBuilder text, int max);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);

    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(nint hwnd, int attribute, out int value, int size);

    [DllImport("kernel32.dll")]
    private static extern nint OpenProcess(uint access, bool inherit, uint processId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern bool QueryFullProcessImageNameW(nint process, uint flags, StringBuilder path, ref uint size);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetApplicationUserModelId(nint process, ref uint length, StringBuilder aumid);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
}
#endif
