using System.Diagnostics;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace CamAda.Assistant;

/// <summary>
/// Ada konuşurken öbür uygulamaların sesini kısar (müzik çalıyorsa Ada duyulsun diye). Oturumun çaldığı cihaza bağlıdır:
/// o cihazdaki uygulama ses oturumlarının düzeyi kaydedilip çarpılır, oturum bitince aynı oturumlara geri yazılır. Böylece
/// oturum sırasında varsayılan çıkış değişse de ("kulaklığa geç") geri yükleme doğru cihaza gider. Arayüz iş parçacığında
/// kurulur ve kullanılır (COM nesneleri o dairede kalır).
/// </summary>
public sealed class AudioDucker(string deviceId)
{
    private readonly List<(AudioSessionControl Session, float Volume)> _saved = [];
    private MMDevice? _device;

    public void Duck(double factor)
    {
        if (_device is not null)
        {
            return; // zaten kısık
        }

        try
        {
            using var enumerator = new MMDeviceEnumerator();
            _device = enumerator.GetDevice(deviceId);
            var sessions = _device.AudioSessionManager.Sessions;
            var self = (uint)Environment.ProcessId;
            for (var i = 0; i < sessions.Count; i++)
            {
                var session = sessions[i];
                if (session.IsSystemSoundsSession || session.GetProcessID == self)
                {
                    continue;
                }

                var volume = session.SimpleAudioVolume.Volume;
                _saved.Add((session, volume));
                session.SimpleAudioVolume.Volume = (float)(volume * factor);
            }
        }
        catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException or ArgumentException)
        {
            Debug.WriteLine($"Ada sesleri kısamadı: {ex.Message}");
        }
    }

    /// <summary>Kaydedilen düzeyleri geri yazar; birden çok kez çağrılabilir.</summary>
    public void Restore()
    {
        foreach (var (session, volume) in _saved)
        {
            try
            {
                session.SimpleAudioVolume.Volume = volume;
            }
            catch (Exception ex) when (ex is COMException or InvalidCastException or UnauthorizedAccessException or ArgumentException)
            {
                Debug.WriteLine($"Ada bir uygulamanın sesini geri açamadı (oturum kapanmış olabilir): {ex.Message}");
            }
        }

        _saved.Clear();
        _device?.Dispose();
        _device = null;
    }
}
