using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>
/// Odağı olmayan bir panelin dışına basılan fare düğmesini yakalar (düşük düzey fare kancası). Yalnız panel odaksızken
/// açık kaldığında (ör. tepsi uygulamasının menüsünden sonra) kurulur; fare olayını engellemez, yalnız haber verir.
/// Kancayı kuran iş parçacığı (UI) mesaj döngüsü çalıştırdığı için geri çağrı orada gelir.
/// </summary>
internal sealed class OutsideClickHook : IDisposable
{
    private const int WH_MOUSE_LL = 14;
    private const int WM_LBUTTONDOWN = 0x0201, WM_RBUTTONDOWN = 0x0204, WM_MBUTTONDOWN = 0x0207;

    private readonly Func<int, int, bool> _isInside;
    private readonly Action _outsideClick;
    private readonly HookProc _callback; // yerel kod tutarken çöp toplayıcı silmesin
    private nint _hook;

    public OutsideClickHook(Func<int, int, bool> isInside, Action outsideClick)
    {
        _isInside = isInside;
        _outsideClick = outsideClick;
        _callback = OnMouse;
        _hook = SetWindowsHookEx(WH_MOUSE_LL, _callback, GetModuleHandle(null), 0);
    }

    public void Dispose()
    {
        if (_hook != 0)
        {
            UnhookWindowsHookEx(_hook);
            _hook = 0;
        }
    }

    private nint OnMouse(int code, nint message, nint data)
    {
        if (code >= 0 && message is WM_LBUTTONDOWN or WM_RBUTTONDOWN or WM_MBUTTONDOWN)
        {
            var info = Marshal.PtrToStructure<MouseHookData>(data);
            if (!_isInside(info.X, info.Y))
            {
                _outsideClick();
            }
        }

        return CallNextHookEx(_hook, code, message, data);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MouseHookData
    {
        public int X, Y;
        public uint MouseData, Flags, Time;
        public nint ExtraInfo;
    }

    private delegate nint HookProc(int code, nint message, nint data);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern nint SetWindowsHookEx(int hookId, HookProc callback, nint module, uint threadId);

    [DllImport("user32.dll")]
    private static extern bool UnhookWindowsHookEx(nint hook);

    [DllImport("user32.dll")]
    private static extern nint CallNextHookEx(nint hook, int code, nint message, nint data);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern nint GetModuleHandle(string? moduleName);
}
