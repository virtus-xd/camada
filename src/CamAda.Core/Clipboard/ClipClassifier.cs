using System.Text.RegularExpressions;

namespace CamAda.Core.Clipboard;

/// <summary>
/// Kopyalanan metnin türü: tek satırlık adres bağlantıdır, koda benzeyen metin koddur, gerisi metindir. Yalnız ilk
/// <see cref="MaxInspected"/> karaktere bakılır (16 MB'lık kopya her eklemede baştan sona bölünmesin).
/// </summary>
public static partial class ClipClassifier
{
    public const int MaxInspected = 16 * 1024;

    public static ClipKind Classify(string text)
    {
        if (text.Length > MaxInspected)
        {
            text = text[..MaxInspected];
        }

        var lines = text.Replace("\r\n", "\n").Split('\n');
        var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        if (nonEmpty.Count == 1)
        {
            var line = nonEmpty[0].Trim();
            if (LinkPattern().IsMatch(line))
            {
                return ClipKind.Link;
            }

            return SingleLineCode().IsMatch(line) || HasClosedBraces(line) || CommandStart().IsMatch(line)
                ? ClipKind.Code
                : ClipKind.Text;
        }

        var codeLike = nonEmpty.Count(l =>
            l.StartsWith('\t') || l.StartsWith("  ", StringComparison.Ordinal) || l.TrimEnd() is var t && (t.EndsWith(';') || t.EndsWith('{') || t.EndsWith('}')));
        return nonEmpty.Count > 1 && codeLike * 3 >= nonEmpty.Count ? ClipKind.Code : ClipKind.Text;
    }

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();

    /// <summary>Satırda bir "{" ve ondan sonra bir "}" var (doğrusal; düzenli ifadeyle kapanışsız uzun satırda karesel sürerdi).</summary>
    private static bool HasClosedBraces(string line)
    {
        var open = line.IndexOf('{');
        return open >= 0 && line.LastIndexOf('}') > open;
    }

    [GeneratedRegex(@"=>|\(\);")]
    private static partial Regex SingleLineCode();

    [GeneratedRegex(@"^(git|dotnet|npm|npx|pip|python|cd|ls|cargo|winget|powershell|pwsh) ", RegexOptions.IgnoreCase)]
    private static partial Regex CommandStart();
}
