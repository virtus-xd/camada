using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>
/// Bekçi süreci, ana süreçle birlikte öldürülemesin diye Explorer'ın alt süreci olarak ve ana sürecin
/// iş nesnesinin (job) dışında başlatır. Böylece "Süreç ağacını sonlandır" ya da kapanınca alt süreçleri
/// öldüren bir başlatıcı bekçiyi de götürmez. Paketliyken Explorer'ın çocuğu yapılan süreç Explorer'ın jetonunu
/// alıp paket kimliğini kaybeder; o yüzden paketliyken Windows'un uygulama etkinleştirmesi kullanılır.
/// </summary>
internal static class DetachedProcess
{
    private const uint PROCESS_CREATE_PROCESS = 0x0080;
    private const uint EXTENDED_STARTUPINFO_PRESENT = 0x00080000;
    private const uint CREATE_BREAKAWAY_FROM_JOB = 0x01000000;
    private const uint CREATE_NO_WINDOW = 0x08000000;
    private static readonly nint PROC_THREAD_ATTRIBUTE_PARENT_PROCESS = 0x00020000;

    /// <summary>
    /// Süreci başlatır ve kimliğini döner. Paketliyken uygulama etkinleştirmesiyle (olmazsa paket kimliğini miras alan
    /// alt süreç olarak), paketsizken Explorer üstünden (olmazsa normal yolla) başlatır.
    /// </summary>
    public static int Start(string exePath, string arguments)
    {
        if (Startup.PackageContext.IsPackaged)
        {
            try
            {
                return Startup.PackagedActivation.Start(arguments);
            }
            catch (Exception ex) when (ex is COMException or InvalidOperationException or UnauthorizedAccessException)
            {
                Debug.WriteLine($"Paketli etkinleştirme başarısız, alt süreç olarak başlatılıyor: {ex.Message}");
            }

            return StartChild(exePath, arguments);
        }

        var explorerPid = GetShellProcessId();
        if (explorerPid != 0)
        {
            foreach (var breakaway in new[] { true, false })
            {
                if (TryStartWithParent(exePath, arguments, explorerPid, breakaway, out var pid))
                {
                    return pid;
                }
            }
        }

        return StartChild(exePath, arguments);
    }

    /// <summary>Kendi alt sürecimiz olarak başlatır (paketliyken paket kimliğini miras alır).</summary>
    private static int StartChild(string exePath, string arguments)
    {
        using var child = Process.Start(new ProcessStartInfo(exePath, arguments) { UseShellExecute = false })
                          ?? throw new Win32Exception("Süreç başlatılamadı.");
        return child.Id;
    }

    private static int GetShellProcessId()
    {
        var shell = GetShellWindow();
        if (shell == 0)
        {
            return 0;
        }

        GetWindowThreadProcessId(shell, out var pid);
        return pid;
    }

    private static bool TryStartWithParent(string exePath, string arguments, int parentPid, bool breakaway, out int pid)
    {
        pid = 0;
        var parent = OpenProcess(PROCESS_CREATE_PROCESS, false, parentPid);
        if (parent == 0)
        {
            return false;
        }

        nint attributeList = 0;
        var parentHandle = Marshal.AllocHGlobal(IntPtr.Size);
        try
        {
            nint size = 0;
            InitializeProcThreadAttributeList(0, 1, 0, ref size);
            attributeList = Marshal.AllocHGlobal(size);
            if (!InitializeProcThreadAttributeList(attributeList, 1, 0, ref size))
            {
                return false;
            }

            Marshal.WriteIntPtr(parentHandle, parent);
            if (!UpdateProcThreadAttribute(attributeList, 0, PROC_THREAD_ATTRIBUTE_PARENT_PROCESS,
                    parentHandle, IntPtr.Size, 0, 0))
            {
                return false;
            }

            var startup = new STARTUPINFOEX { lpAttributeList = attributeList };
            startup.StartupInfo.cb = Marshal.SizeOf<STARTUPINFOEX>();
            var flags = EXTENDED_STARTUPINFO_PRESENT | CREATE_NO_WINDOW | (breakaway ? CREATE_BREAKAWAY_FROM_JOB : 0);
            var commandLine = $"\"{exePath}\" {arguments}";

            if (!CreateProcess(exePath, commandLine, 0, 0, false, flags, 0,
                    Path.GetDirectoryName(exePath), ref startup, out var info))
            {
                return false;
            }

            CloseHandle(info.hThread);
            CloseHandle(info.hProcess);
            pid = info.dwProcessId;
            return true;
        }
        finally
        {
            if (attributeList != 0)
            {
                DeleteProcThreadAttributeList(attributeList);
                Marshal.FreeHGlobal(attributeList);
            }

            Marshal.FreeHGlobal(parentHandle);
            CloseHandle(parent);
        }
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct STARTUPINFO
    {
        public int cb;
        public string? lpReserved, lpDesktop, lpTitle;
        public int dwX, dwY, dwXSize, dwYSize, dwXCountChars, dwYCountChars, dwFillAttribute, dwFlags;
        public short wShowWindow, cbReserved2;
        public nint lpReserved2, hStdInput, hStdOutput, hStdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct STARTUPINFOEX
    {
        public STARTUPINFO StartupInfo;
        public nint lpAttributeList;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct PROCESS_INFORMATION
    {
        public nint hProcess, hThread;
        public int dwProcessId, dwThreadId;
    }

    [DllImport("user32.dll")]
    private static extern nint GetShellWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(nint hwnd, out int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint OpenProcess(uint access, bool inherit, int processId);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool InitializeProcThreadAttributeList(nint list, int count, int flags, ref nint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool UpdateProcThreadAttribute(nint list, uint flags, nint attribute, nint value,
        nint size, nint previousValue, nint returnSize);

    [DllImport("kernel32.dll")]
    private static extern void DeleteProcThreadAttributeList(nint list);

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcess(string application, string commandLine, nint processAttributes,
        nint threadAttributes, bool inheritHandles, uint creationFlags, nint environment, string? currentDirectory,
        ref STARTUPINFOEX startupInfo, out PROCESS_INFORMATION processInformation);

    [DllImport("kernel32.dll")]
    private static extern bool CloseHandle(nint handle);
}
