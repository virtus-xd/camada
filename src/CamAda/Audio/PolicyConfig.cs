using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace CamAda.Audio;

/// <summary>
/// Varsayılan ses cihazını değiştirmek için Windows'un belgelenmemiş ama Windows 7'den beri değişmeyen IPolicyConfig
/// arayüzü (EarTrumpet, SoundSwitch gibi araçlar da bunu kullanır). Yalnız SetDefaultEndpoint çağrılır; diğer yöntemler
/// sanal tablo sırasını korumak için yer tutucudur.
/// </summary>
internal static class PolicyConfig
{
    /// <summary>Cihazı üç rolün (konsol, multimedya, iletişim) varsayılanı yapar.</summary>
    public static void SetDefaultEndpoint(string deviceId)
    {
        var config = (IPolicyConfig)new CPolicyConfigClient();
        try
        {
            foreach (var role in new[] { Role.Console, Role.Multimedia, Role.Communications })
            {
                Marshal.ThrowExceptionForHR(config.SetDefaultEndpoint(deviceId, role));
            }
        }
        finally
        {
            Marshal.ReleaseComObject(config);
        }
    }

    [ComImport]
    [Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
    private class CPolicyConfigClient
    {
    }

    [ComImport]
    [Guid("f8679f50-850a-41cf-9c72-430f290290c8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IPolicyConfig
    {
        [PreserveSig] int GetMixFormat();
        [PreserveSig] int GetDeviceFormat();
        [PreserveSig] int ResetDeviceFormat();
        [PreserveSig] int SetDeviceFormat();
        [PreserveSig] int GetProcessingPeriod();
        [PreserveSig] int SetProcessingPeriod();
        [PreserveSig] int GetShareMode();
        [PreserveSig] int SetShareMode();
        [PreserveSig] int GetPropertyValue();
        [PreserveSig] int SetPropertyValue();
        [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, Role role);
        [PreserveSig] int SetEndpointVisibility();
    }
}
