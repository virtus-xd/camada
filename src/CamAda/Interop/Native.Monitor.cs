using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>
/// Monitör listesi ve harici monitörlerin DDC/CI parlaklığı (belgelenmiş user32 ve dxva2 çağrıları; Plan 4.3).
/// </summary>
internal static class MonitorApi
{
    public const uint EDD_GET_DEVICE_INTERFACE_NAME = 1;

    public delegate bool MonitorEnumProc(nint hMonitor, nint hdc, nint rect, nint data);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct MONITORINFOEX
    {
        public int cbSize;
        public RECT rcMonitor;
        public RECT rcWork;
        public uint dwFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string szDevice;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct DISPLAY_DEVICE
    {
        public int cb;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
        public string DeviceName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceString;

        public int StateFlags;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceID;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string DeviceKey;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    public struct PHYSICAL_MONITOR
    {
        public nint hPhysicalMonitor;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayMonitors(nint hdc, nint clip, MonitorEnumProc proc, nint data);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorInfo(nint hMonitor, ref MONITORINFOEX info);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool EnumDisplayDevices(string? device, uint devNum, ref DISPLAY_DEVICE displayDevice, uint flags);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(nint hMonitor, out uint count);

    [DllImport("dxva2.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetPhysicalMonitorsFromHMONITOR(nint hMonitor, uint count, [Out] PHYSICAL_MONITOR[] monitors);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool GetMonitorBrightness(nint physicalMonitor, out uint minimum, out uint current, out uint maximum);

    [DllImport("dxva2.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool SetMonitorBrightness(nint physicalMonitor, uint brightness);

    [DllImport("dxva2.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyPhysicalMonitors(uint count, PHYSICAL_MONITOR[] monitors);

    /// <summary>Tüm ekranların HMONITOR'ları ve aygıt adları (\\.\DISPLAY1 …).</summary>
    public static IReadOnlyList<(nint Handle, string Device)> Monitors()
    {
        var list = new List<(nint, string)>();
        EnumDisplayMonitors(0, 0, (hMonitor, _, _, _) =>
        {
            var info = new MONITORINFOEX { cbSize = Marshal.SizeOf<MONITORINFOEX>() };
            if (GetMonitorInfo(hMonitor, ref info))
            {
                list.Add((hMonitor, info.szDevice));
            }

            return true;
        }, 0);
        return list;
    }

    /// <summary>
    /// Ekran aygıtının WMI örnek adıyla karşılaştırılabilir kimliği: arayüz yolu "\\?\DISPLAY#LEN9056#5&amp;…&amp;UID256#{…}"
    /// → "DISPLAY\LEN9056\5&amp;…&amp;UID256". Bulunamazsa null.
    /// </summary>
    public static string? InstanceIdOf(string device)
    {
        var dd = new DISPLAY_DEVICE { cb = Marshal.SizeOf<DISPLAY_DEVICE>() };
        if (!EnumDisplayDevices(device, 0, ref dd, EDD_GET_DEVICE_INTERFACE_NAME) || string.IsNullOrEmpty(dd.DeviceID))
        {
            return null;
        }

        var path = dd.DeviceID;
        var start = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? 4 : 0;
        var end = path.IndexOf("#{", StringComparison.Ordinal);
        return end > start ? path[start..end].Replace('#', '\\') : null;
    }
}
