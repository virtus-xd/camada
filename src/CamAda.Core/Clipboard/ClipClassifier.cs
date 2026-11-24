using System.Text.RegularExpressions;

namespace CamAda.Core.Clipboard;

/// <summary>Kopyalanan metnin türü: tek satırlık adres bağlantıdır, koda benzeyen metin koddur, gerisi metindir.</summary>
public static partial class ClipClassifier
{
    public static ClipKind Classify(string text)
    {
        var lines = text.Replace("\r\n", "\n").Split('\n');
        var nonEmpty = lines.Where(l => !string.IsNullOrWhiteSpace(l)).ToList();

        if (nonEmpty.Count == 1)
        {
            var line = nonEmpty[0].Trim();
            if (LinkPattern().IsMatch(line))
            {
                return ClipKind.Link;
            }

            return SingleLineCode().IsMatch(line) || CommandStart().IsMatch(line) ? ClipKind.Code : ClipKind.Text;
        }

        var codeLike = nonEmpty.Count(l =>
            l.StartsWith('\t') || l.StartsWith("  ", StringComparison.Ordinal) || l.TrimEnd() is var t && (t.EndsWith(';') || t.EndsWith('{') || t.EndsWith('}')));
        return nonEmpty.Count > 1 && codeLike * 3 >= nonEmpty.Count ? ClipKind.Code : ClipKind.Text;
    }

    [GeneratedRegex(@"^https?://\S+$", RegexOptions.IgnoreCase)]
    private static partial Regex LinkPattern();

    [GeneratedRegex(@"=>|\(\);|\{.*\}")]
    private static partial Regex SingleLineCode();

    [GeneratedRegex(@"^(git|dotnet|npm|npx|pip|python|cd|ls|cargo|winget|powershell|pwsh) ", RegexOptions.IgnoreCase)]
    private static partial Regex CommandStart();
}
