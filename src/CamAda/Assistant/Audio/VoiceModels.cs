using System.IO;

namespace CamAda.Assistant.Audio;

/// <summary>
/// Misafir kipinin yerel modelleri (<c>Assets\Speaker</c>): konuşmacı gömme modeli ve Silero konuşma algılayıcısı.
/// Eksik dosyayla sherpa-onnx süreci düşürebildiği için kullanmadan önce <see cref="Present"/> denetlenir.
/// </summary>
internal static class VoiceModels
{
    public const string SpeakerModel = "3dspeaker_speech_campplus_sv_zh_en_16k-common_advanced.onnx";
    public const string Vad = "silero_vad.onnx";

    public static string Dir => Path.Combine(AppContext.BaseDirectory, "Assets", "Speaker");

    public static bool Present(string dir) =>
        File.Exists(Path.Combine(dir, SpeakerModel)) && File.Exists(Path.Combine(dir, Vad));
}
