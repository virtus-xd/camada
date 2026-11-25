using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace CamAda.Assistant.Audio;

/// <summary>Ada'nın mikrofon ve hoparlör seçimi, yankı engelleyicinin istediği cihaz dizinleri.</summary>
internal static class MicDevices
{
    /// <summary>
    /// Ad parçası verilmişse onu içeren ilk etkin kayıt cihazı; yoksa Windows'un varsayılan iletişim mikrofonu. Hiç
    /// mikrofon yoksa null.
    /// </summary>
    public static MMDevice? ResolveMic(string? nameHint)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            if (!string.IsNullOrWhiteSpace(nameHint))
            {
                var match = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active)
                    .FirstOrDefault(d => d.FriendlyName.Contains(nameHint, StringComparison.CurrentCultureIgnoreCase));
                if (match is not null)
                {
                    return match;
                }
            }

            return enumerator.HasDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                ? enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications)
                : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    public static MMDevice? DefaultSpeaker()
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.HasDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                ? enumerator.GetDefaultAudioEndpoint(DataFlow.Render, Role.Multimedia)
                : null;
        }
        catch (COMException)
        {
            return null;
        }
    }

    /// <summary>Yankı engelleyicinin cihaz dizinleri: etkin uç listelerindeki sıra; bulunamazsa -1.</summary>
    public static (int Capture, int Render) DspIndexes(MMDevice mic, MMDevice speaker) => DspIndexes(mic.ID, speaker.ID);

    public static (int Capture, int Render) DspIndexes(string micId, string speakerId)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            var captures = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active).Select(d => d.ID).ToList();
            var renders = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).Select(d => d.ID).ToList();
            return (captures.IndexOf(micId), renders.IndexOf(speakerId));
        }
        catch (COMException)
        {
            return (-1, -1);
        }
    }

    /// <summary>Kimliğiyle cihaz (çağıran iş parçacığının dairesinde); yoksa null.</summary>
    public static MMDevice? ById(string id)
    {
        try
        {
            using var enumerator = new MMDeviceEnumerator();
            return enumerator.GetDevice(id);
        }
        catch (COMException)
        {
            return null;
        }
    }
}
