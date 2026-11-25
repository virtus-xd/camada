#if DEBUG
using CamAda.Assistant.Audio;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// Uyandırma dinleyicisini arayüz iş parçacığında 300 kez başlatıp durdurur (kayıt sürerken). Durdurma kilit tutarken
    /// yakalamayı atsaydı, kayıt iş parçacığı o kilidi beklerken arayüz sonsuza dek beklerdi; deneme o zaman bitmez.
    /// </summary>
    private static async Task WakeStressAsync()
    {
        var settings = Settings();
        using var listener = new WakeWordListener(ModelDir, settings.WakeThreshold, () => MicDevices.ResolveMic(settings.Microphone));
        var random = new Random(1);
        var started = DateTime.UtcNow;
        Log("ada-uyandirma-stres: başladı");
        for (var i = 1; i <= 300; i++)
        {
            listener.Start();
            await Task.Delay(random.Next(5, 70));
            listener.Stop(); // arayüz iş parçacığında, eşzamanlı
            if (i % 50 == 0)
            {
                Log($"döngü {i}");
            }
        }

        Log($"ada-uyandirma-stres: bitti, {(DateTime.UtcNow - started).TotalSeconds:F1} sn");
    }

    /// <summary>Kısma ve geri yükleme, oturum ve mikrofon açmadan: varsayılan çıkıştaki uygulama düzeyleri önce/kısık/sonra.</summary>
    private static void DuckProbe()
    {
        using var audio = new CamAda.Audio.AudioService(System.Windows.Threading.Dispatcher.CurrentDispatcher);
        string Volumes() => string.Join(", ", audio.Apps().Select(a => $"{a.Name} %{Math.Round(a.Volume * 100)}"));
        var speaker = MicDevices.DefaultSpeaker();
        if (speaker is null)
        {
            Log("ada-kisma-sina: hoparlör yok");
            return;
        }

        var ducker = new AudioDucker(speaker.ID);
        Log($"ada-kisma-sina: önce [{Volumes()}]");
        ducker.Duck(0.3);
        Log($"kısık [{Volumes()}]");
        ducker.Restore();
        Log($"geri [{Volumes()}]");
        ducker.Restore(); // ikinci çağrı zararsız olmalı
        Log($"ikinci geri yüklemeden sonra [{Volumes()}]");
    }
}
#endif
