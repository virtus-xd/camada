using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>Önceki pencereye yapıştırma (Plan 4.2): ön pencere, yönetici denetimi, basılı tuşlar ve Ctrl+V gönderimi.</summary>
internal static partial class Native
{
    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const ushort VK_RETURN = 0x0D;
    private const ushort VK_CONTROL = 0x11;
    private const ushort VK_V = 0x56;
    private const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;
    private const uint TOKEN_QUERY = 0x0008;
    private const int TokenElevation = 20;

    /// <summary>Win, Alt, Ctrl ve Shift (sol/sağ ayrımı GetAsyncKeyState'te genel kodlarla da görünür).</summary>
    private static readonly int[] Modifiers = [0x5B, 0x5C, 0x12, 0x11, 0x10];

    [LibraryImport("user32.dll")]
    public static partial nint GetForegroundWindow();

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("user32.dll")]
    private static partial short GetAsyncKeyState(int virtualKey);

    [LibraryImport("user32.dll", SetLastError = true)]
    private static partial uint SendInput(uint count, [In] INPUT[] inputs, int size);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    private static partial nint OpenProcess(uint access, [MarshalAs(UnmanagedType.Bool)] bool inherit, int processId);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool OpenProcessToken(nint process, uint access, out nint token);

    [LibraryImport("advapi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetTokenInformation(nint token, int infoClass, out int info, int length, out int returned);

    [LibraryImport("kernel32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool CloseHandle(nint handle);

    public static int ProcessIdOf(nint hwnd)
    {
        GetWindowThreadProcessId(hwnd, out var processId);
        return processId;
    }

    public static string ClassNameOf(nint hwnd)
    {
        var buffer = new char[256];
        var length = GetClassName(hwnd, buffer, buffer.Length);
        return new string(buffer, 0, Math.Max(0, length));
    }

    /// <summary>Alt ya da Win şu an basılı mı.</summary>
    public static bool AltOrWinDown() => Modifiers.Take(3).Any(vk => (GetAsyncKeyState(vk) & 0x8000) != 0);

    /// <summary>
    /// Anlamsız bir tuş (VK 0xE8, atanmamış) bas-bırak. Alt basılıyken öne gelen pencere, Alt bırakılınca onu "tek başına
    /// basılmış Alt" sayıp sistem menüsü moduna geçer ve ardından gelen Ctrl+V'yi yutar; araya giren bu tuş bunu önler.
    /// </summary>
    public static void SendMaskKey()
    {
        INPUT[] inputs = [Key(0xE8, up: false), Key(0xE8, up: true)];
        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
    }

    /// <summary>Win, Alt, Ctrl ya da Shift şu an basılı mı.</summary>
    public static bool AnyModifierDown() => Modifiers.Any(vk => (GetAsyncKeyState(vk) & 0x8000) != 0);

    /// <summary>
    /// Süreç yönetici olarak mı çalışıyor (Windows, yönetici olmayan CamAda'nın ona tuş göndermesine izin vermez).
    /// Belirteci okunamıyorsa da öyle sayılır: yanlışlıkla tuş göndermektense yalnız kopyalamak güvenlidir.
    /// </summary>
    public static bool IsElevated(int processId)
    {
        var process = OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION, false, processId);
        if (process == 0)
        {
            return true;
        }

        try
        {
            if (!OpenProcessToken(process, TOKEN_QUERY, out var token))
            {
                return true;
            }

            try
            {
                return !GetTokenInformation(token, TokenElevation, out var elevated, sizeof(int), out _) || elevated != 0;
            }
            finally
            {
                CloseHandle(token);
            }
        }
        finally
        {
            CloseHandle(process);
        }
    }

    /// <summary>Ön pencereye Ctrl+V gönderir (Ctrl↓ V↓ V↑ Ctrl↑).</summary>
    public static bool SendCtrlV()
    {
        INPUT[] inputs =
        [
            Key(VK_CONTROL, up: false),
            Key(VK_V, up: false),
            Key(VK_V, up: true),
            Key(VK_CONTROL, up: true),
        ];
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
    }

    /// <summary>
    /// Metni ön pencereye tuş olarak yazar (Ada'nın notu): her karakter Unicode tuşu, satır sonu Enter; pano kullanılmaz.
    /// Tümü gönderildiyse true.
    /// </summary>
    public static bool TypeText(string text)
    {
        var inputs = new List<INPUT>(text.Length * 2);
        foreach (var c in text)
        {
            if (c == '\r')
            {
                continue;
            }

            if (c == '\n')
            {
                inputs.Add(Key(VK_RETURN, up: false));
                inputs.Add(Key(VK_RETURN, up: true));
                continue;
            }

            inputs.Add(Unicode(c, up: false));
            inputs.Add(Unicode(c, up: true));
        }

        return inputs.Count == 0 || SendInput((uint)inputs.Count, inputs.ToArray(), Marshal.SizeOf<INPUT>()) == inputs.Count;
    }

    /// <summary>Tuşlara sırayla basar, ters sırayla bırakır (ör. Ctrl+W).</summary>
    public static bool SendChord(params ushort[] keys)
    {
        var inputs = keys.Select(k => Key(k, up: false)).Concat(keys.Reverse().Select(k => Key(k, up: true))).ToArray();
        return SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>()) == inputs.Length;
    }

    private static INPUT Unicode(char c, bool up) => new()
    {
        Type = INPUT_KEYBOARD,
        Union = new InputUnion { Keyboard = new KEYBDINPUT { ScanCode = c, Flags = KEYEVENTF_UNICODE | (up ? KEYEVENTF_KEYUP : 0) } },
    };

    private static INPUT Key(ushort virtualKey, bool up) => new()
    {
        Type = INPUT_KEYBOARD,
        Union = new InputUnion { Keyboard = new KEYBDINPUT { VirtualKey = virtualKey, Flags = up ? KEYEVENTF_KEYUP : 0 } },
    };

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint Type;
        public InputUnion Union;
    }

    [StructLayout(LayoutKind.Explicit)]
    private struct InputUnion
    {
        [FieldOffset(0)] public MOUSEINPUT Mouse;
        [FieldOffset(0)] public KEYBDINPUT Keyboard;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int X;
        public int Y;
        public uint MouseData;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort VirtualKey;
        public ushort ScanCode;
        public uint Flags;
        public uint Time;
        public nint ExtraInfo;
    }
}
