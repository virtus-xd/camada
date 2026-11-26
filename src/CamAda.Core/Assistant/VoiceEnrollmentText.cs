namespace CamAda.Core.Assistant;

/// <summary>Sesini tanıtırken okunan cümleler (misafir kipi): farklı sesler ve bir de Ada'ya seslenme.</summary>
public static class VoiceEnrollmentText
{
    public static IReadOnlyList<string> Sentences { get; } =
    [
        "Bugün hava güzel, biraz yürüyüşe çıkabilirim.",
        "Ada, sesi biraz kıs ve müziği duraklat.",
        "Akşam yemeğinden sonra kitabımı okumak istiyorum.",
        "Yarın sabah erkenden kalkıp işe gideceğim.",
        "Bilgisayarın parlaklığını yüzde elliye ayarla.",
    ];
}
