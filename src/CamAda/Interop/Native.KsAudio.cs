using System.Runtime.InteropServices;

namespace CamAda.Interop;

/// <summary>
/// Bluetooth ses cihazını bağlama/ayırma için Core Audio ve çekirdek akış arayüzleri (belgelenmiş; Plan 4.3 Görev 1
/// spike'ında doğrulandı): ses ucu → IDeviceTopology → bağlayıcı → karşı IPart → filtre aygıtı → IKsControl →
/// KSPROPSETID_BtAudio. Yalnız kullanılan yöntemler tanımlı; sanal tablo sırası korunur.
/// </summary>
internal static class KsAudio
{
    public const int ERender = 0;
    public const int DeviceStateActive = 0x1;
    public const int DeviceStateUnplugged = 0x8;
    public const int ClsctxAll = 0x17;
    public const uint KsPropertyTypeGet = 1;
    public const uint OneShotReconnect = 0;
    public const uint OneShotDisconnect = 1;

    public static readonly Guid KsPropSetBtAudio = new("7FA06C40-B8F6-4C7E-8556-E8C33A12E54D");
    public static readonly PROPERTYKEY ContainerIdKey = new(new Guid("8c7ed206-3f8a-4827-b3ab-ae9e1faefc6c"), 2);

    [StructLayout(LayoutKind.Sequential)]
    public struct PROPERTYKEY(Guid fmtid, int pid)
    {
        public Guid Fmtid = fmtid;
        public int Pid = pid;
    }

    [StructLayout(LayoutKind.Explicit, Size = 24)]
    public struct PROPVARIANT
    {
        [FieldOffset(0)]
        public ushort Vt;

        [FieldOffset(8)]
        public nint Pointer;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct KSPROPERTY
    {
        public Guid Set;
        public uint Id;
        public uint Flags;
    }

    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PROPVARIANT value);

    [ComImport]
    [Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
    public class MMDeviceEnumerator
    {
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceEnumerator
    {
        [PreserveSig]
        int EnumAudioEndpoints(int dataFlow, int stateMask, out IMMDeviceCollection devices);

        [PreserveSig]
        int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice device);

        [PreserveSig]
        int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }

    [ComImport]
    [Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDeviceCollection
    {
        [PreserveSig]
        int GetCount(out int count);

        [PreserveSig]
        int Item(int index, out IMMDevice device);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IMMDevice
    {
        [PreserveSig]
        int Activate(ref Guid iid, int clsCtx, nint activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object instance);

        [PreserveSig]
        int OpenPropertyStore(int access, out IPropertyStore properties);
    }

    [ComImport]
    [Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPropertyStore
    {
        [PreserveSig]
        int GetCount(out int count);

        [PreserveSig]
        int GetAt(int index, out PROPERTYKEY key);

        [PreserveSig]
        int GetValue(ref PROPERTYKEY key, out PROPVARIANT value);
    }

    [ComImport]
    [Guid("2A07407E-6497-4A18-9787-32F79BD0D98F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDeviceTopology
    {
        [PreserveSig]
        int GetConnectorCount(out uint count);

        [PreserveSig]
        int GetConnector(uint index, out IConnector connector);

        [PreserveSig]
        int GetSubunitCount(out uint count);

        [PreserveSig]
        int GetSubunit(uint index, out nint subunit);

        [PreserveSig]
        int GetPartById(uint id, out IPart part);

        [PreserveSig]
        int GetDeviceId([MarshalAs(UnmanagedType.LPWStr)] out string id);
    }

    [ComImport]
    [Guid("9C2C4058-23F5-41DE-877A-DF3AF236A09E")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IConnector
    {
        [PreserveSig]
        int GetType(out int type);

        [PreserveSig]
        int GetDataFlow(out int flow);

        [PreserveSig]
        int ConnectTo(IConnector to);

        [PreserveSig]
        int Disconnect();

        [PreserveSig]
        int IsConnected([MarshalAs(UnmanagedType.Bool)] out bool connected);

        [PreserveSig]
        int GetConnectedTo(out IConnector to);
    }

    [ComImport]
    [Guid("AE2DE0E4-5BCA-4F2D-AA46-5D13F8FDB3A9")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IPart
    {
        [PreserveSig]
        int GetName([MarshalAs(UnmanagedType.LPWStr)] out string name);

        [PreserveSig]
        int GetLocalId(out uint id);

        [PreserveSig]
        int GetGlobalId([MarshalAs(UnmanagedType.LPWStr)] out string id);

        [PreserveSig]
        int GetPartType(out int type);

        [PreserveSig]
        int GetSubType(out Guid subtype);

        [PreserveSig]
        int GetControlInterfaceCount(out uint count);

        [PreserveSig]
        int GetControlInterface(uint index, out nint control);

        [PreserveSig]
        int EnumPartsIncoming(out nint parts);

        [PreserveSig]
        int EnumPartsOutgoing(out nint parts);

        [PreserveSig]
        int GetTopologyObject(out IDeviceTopology topology);
    }

    [ComImport]
    [Guid("28F54685-06FD-11D2-B27A-00A0C9223196")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IKsControl
    {
        [PreserveSig]
        int KsProperty(ref KSPROPERTY property, int propertyLength, nint propertyData, int dataLength, out int bytesReturned);
    }
}
