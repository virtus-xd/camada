using System.Globalization;
using CamAda.Core.Text;

namespace CamAda.Core.Assistant;

/// <summary>Sesini tanıtırken okunan cümleler (misafir kipi): farklı sesler ve bir de Ada'ya seslenme.</summary>
public static class VoiceEnrollmentText
{
    private static readonly string[] Keys =
    [
        nameof(CoreStrings.Enroll_Sentence1),
        nameof(CoreStrings.Enroll_Sentence2),
        nameof(CoreStrings.Enroll_Sentence3),
        nameof(CoreStrings.Enroll_Sentence4),
        nameof(CoreStrings.Enroll_Sentence5),
    ];

    /// <summary>Arayüz dilindeki cümleler (ses izi dilden bağımsız; kullanıcı kendi dilinde okur).</summary>
    public static IReadOnlyList<string> Sentences(CultureInfo ui) => Keys.Select(k => CoreText.Get(k, ui)).ToArray();
}
