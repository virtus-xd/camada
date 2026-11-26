namespace CamAda.Core.Assistant;

public enum SentenceCheck
{
    Ok,
    TooQuiet,
    TooShort,
}

/// <summary>
/// Sesini tanıtırken okunan bir cümlenin yeterli olup olmadığı (misafir kipi). Sınırlar Plan 8 ölçümünden: kullanıcının normal
/// konuşması 0,008–0,03 RMS, süzgecin kısık ses sınırı 0,006; gömme 1 sn'den kısa seste güvenilmez.
/// </summary>
public static class EnrollmentCheck
{
    public const double MinRms = 0.006;
    public const int MinSpeechSamples = 16000;

    /// <param name="speechSamples">Konuşma algılayıcının konuşma saydığı örnek sayısı (16 kHz).</param>
    /// <param name="speechRms">Konuşma kısmının RMS'i (0–1).</param>
    public static SentenceCheck Check(int speechSamples, double speechRms) =>
        speechRms < MinRms ? SentenceCheck.TooQuiet
        : speechSamples < MinSpeechSamples ? SentenceCheck.TooShort
        : SentenceCheck.Ok;
}
