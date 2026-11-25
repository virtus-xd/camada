using NAudio.CoreAudioApi;

namespace CamAda.Assistant.Audio;

/// <summary>
/// Ada'nın mikrofonlarını kurar. Debug'da <c>CAMADA_ADA_FAKE_ROOM</c> bir WAV dosyası gösteriyorsa gerçek mikrofon hiç
/// açılmaz: her mikrofon o dosyayı ortak bir "oda" zaman çizgisinden duyar (oturum denemeleri odadaki konuşmayı
/// yakalamasın diye).
/// </summary>
internal static class MicFactory
{
    public static IAdaMic Plain(MMDevice device)
    {
#if DEBUG
        if (FakeRoom.Active)
        {
            return new FakeMic(echoCancelled: false);
        }
#endif
        return new PlainMic(device);
    }

    public static IAdaMic EchoCancelled(int captureIndex, int renderIndex)
    {
#if DEBUG
        if (FakeRoom.Active)
        {
            return new FakeMic(echoCancelled: true);
        }
#endif
        return new EchoCancelledMic(captureIndex, renderIndex);
    }
}
