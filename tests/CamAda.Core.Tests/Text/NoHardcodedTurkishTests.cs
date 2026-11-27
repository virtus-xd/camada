using System.Text.RegularExpressions;

namespace CamAda.Core.Tests.Text;

/// <summary>
/// Mandal: arayüz metinleri kodda sabit yazılmaz, .resx'ten gelir. C#'ta Türkçe harfli dize sabiti, XAML'da (dilden bağımsız)
/// düz metin aranır. <see cref="Pending"/> henüz çevrilmemiş dosyalardır; her görev kendi dosyalarını listeden siler.
/// <see cref="Permanent"/> Ada'nın modele giden metinleri (2. adım) ve geliştirici deneme komutlarıdır.
/// </summary>
public partial class NoHardcodedTurkishTests
{
    private static readonly string[] Permanent =
    [
        "src/CamAda.Core/Assistant/AdaInstruction.cs",
        "src/CamAda.Core/Assistant/AdaTools.cs",
        "src/CamAda.Core/Assistant/AppAliases.cs",
        "src/CamAda.Core/Assistant/NameMatch.cs",
        "src/CamAda.Core/Assistant/SensitiveText.cs",
        "src/CamAda.Core/Assistant/MemoryBook.cs",
        "src/CamAda.Core/Assistant/Reminders.cs",
        "src/CamAda.Core/Assistant/ToolArgs.cs",
        "src/CamAda.Core/Weather/WeatherAnswer.cs",
        "src/CamAda/Assistant/AdaToolRunner.cs",
        "src/CamAda/Assistant/CurrentInfoClient.cs",
        "src/CamAda/Assistant/AdaSpeech.cs",
        "src/CamAda/Weather/WeatherLookup.cs",
    ];

    private static readonly string[] PermanentPrefixes = ["src/CamAda/Assistant/AdaProbes"];

    private static readonly string[] Pending =
    [
        "src/CamAda.Core/Assistant/AdaText.cs",
        "src/CamAda.Core/Assistant/VoiceEnrollmentText.cs",
        "src/CamAda/Assistant/AdaAssistant.cs",
        "src/CamAda/Assistant/AdaState.cs",
        "src/CamAda/Assistant/Audio/EchoCancelledMic.cs",
        "src/CamAda/Assistant/Audio/FakeRoom.cs",
        "src/CamAda/Assistant/Audio/VoiceFilter.cs",
        "src/CamAda/Assistant/MemoryFile.cs",
        "src/CamAda/Assistant/NotepadWriter.cs",
        "src/CamAda/Assistant/ReminderFile.cs",
        "src/CamAda/Panels/AdaPanel.xaml",
        "src/CamAda/Panels/AdaPanel.xaml.cs",
        "src/CamAda/Spotify/LoopbackCallback.cs",
        "src/CamAda/Spotify/SpotifyConnectView.xaml",
        "src/CamAda/Spotify/SpotifyConnectView.xaml.cs",
    ];

    private static readonly HashSet<string> Brands = new(StringComparer.Ordinal)
    {
        "Wi‑Fi", "Wi-Fi", "Bluetooth", "Spotify", "Camada", "Ada",
    };

    [Fact]
    public void Only_pending_or_permanent_files_contain_hardcoded_ui_text()
    {
        var violations = Scan().Where(v => !IsPermanent(v.File) && !Pending.Contains(v.File)).ToList();
        var files = violations.Select(v => v.File).Distinct().Order(StringComparer.Ordinal);
        var lines = violations.Select(v => $"{v.File}:{v.Line}: {v.Text}");
        Assert.True(violations.Count == 0,
            "Kodda sabit arayüz metni. Dosyalar:\n" + string.Join("\n", files) + "\n\nSatırlar:\n" + string.Join("\n", lines.Take(400)));
    }

    [Fact]
    public void Pending_list_stays_tight()
    {
        var withViolations = Scan().Select(v => v.File).ToHashSet();
        var cleared = Pending.Where(f => !withViolations.Contains(f)).ToList();
        Assert.True(cleared.Count == 0, "Temizlenen dosyaları Pending listesinden silin:\n" + string.Join("\n", cleared));
    }

    private static bool IsPermanent(string file) =>
        Permanent.Contains(file) || PermanentPrefixes.Any(p => file.StartsWith(p, StringComparison.Ordinal));

    private sealed record Violation(string File, int Line, string Text);

    private static List<Violation> Scan()
    {
        var root = RepoRoot.Find();
        var result = new List<Violation>();
        foreach (var dir in new[] { "src/CamAda", "src/CamAda.Core" })
        {
            var full = Path.Combine(root, dir.Replace('/', Path.DirectorySeparatorChar));
            foreach (var path in Directory.EnumerateFiles(full, "*.*", SearchOption.AllDirectories))
            {
                var rel = Path.GetRelativePath(root, path).Replace('\\', '/');
                if (rel.Contains("/bin/", StringComparison.Ordinal) || rel.Contains("/obj/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (rel.EndsWith(".cs", StringComparison.Ordinal))
                {
                    result.AddRange(ScanCSharp(rel, File.ReadAllLines(path)));
                }
                else if (rel.EndsWith(".xaml", StringComparison.Ordinal))
                {
                    result.AddRange(ScanXaml(rel, File.ReadAllText(path)));
                }
            }
        }

        return result;
    }

    private static IEnumerable<Violation> ScanCSharp(string file, string[] lines)
    {
        var inRaw = false;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var rawMarks = CountRawMarks(line);
            if (inRaw || rawMarks > 0)
            {
                if (TurkishLetter().IsMatch(StripLineComment(line)))
                {
                    yield return new Violation(file, i + 1, line.Trim());
                }

                if (rawMarks % 2 == 1)
                {
                    inRaw = !inRaw;
                }

                continue;
            }

            var code = StripLineComment(line);
            if (code.TrimStart().StartsWith("//", StringComparison.Ordinal) || IsDiagnostic(line))
            {
                continue;
            }

            foreach (Match m in StringLiteral().Matches(code))
            {
                if (TurkishLetter().IsMatch(m.Value))
                {
                    yield return new Violation(file, i + 1, m.Value);
                    break;
                }
            }
        }
    }

    /// <summary>
    /// Ekranda görünmeyen satırlar: geliştirici günlüğü, içeride fırlatılan hata ve gerekçesiyle işaretlenmiş veri
    /// (<c>// i18n-ignore: neden</c>, ör. eşleştirme sözcükleri ya da modele giden metin).
    /// </summary>
    private static bool IsDiagnostic(string line) =>
        line.Contains("Debug.Write", StringComparison.Ordinal) || line.Contains("Trace.Write", StringComparison.Ordinal)
        || line.Contains("Console.Write", StringComparison.Ordinal) || line.Contains("throw new", StringComparison.Ordinal)
        || line.Contains("i18n-ignore", StringComparison.Ordinal);

    private static int CountRawMarks(string line)
    {
        var count = 0;
        for (var at = line.IndexOf("\"\"\"", StringComparison.Ordinal); at >= 0; at = line.IndexOf("\"\"\"", at + 3, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    /// <summary>Dize dışındaki ilk // ve sonrasını atar (yaklaşık: kaçışlı tırnakları sayar).</summary>
    private static string StripLineComment(string line)
    {
        var inString = false;
        for (var i = 0; i < line.Length - 1; i++)
        {
            var c = line[i];
            if (c == '\\' && inString)
            {
                i++;
                continue;
            }

            if (c == '"')
            {
                inString = !inString;
            }
            else if (!inString && c == '/' && line[i + 1] == '/')
            {
                return line[..i];
            }
        }

        return line;
    }

    private static IEnumerable<Violation> ScanXaml(string file, string text)
    {
        var noComments = XmlComment().Replace(text, m => new string('\n', m.Value.Count(ch => ch == '\n')));
        foreach (Match m in XamlTextAttribute().Matches(noComments))
        {
            var value = m.Groups["value"].Value;
            if (!value.StartsWith('{') && IsReadableText(value))
            {
                yield return new Violation(file, LineOf(noComments, m.Index), m.Value);
            }
        }

        foreach (Match m in XamlElementText().Matches(noComments))
        {
            var value = m.Groups["value"].Value.Trim();
            if (value.Length > 0 && IsReadableText(value))
            {
                yield return new Violation(file, LineOf(noComments, m.Index), value);
            }
        }
    }

    private static bool IsReadableText(string value)
    {
        var plain = Entity().Replace(value, "").Trim();
        return !Brands.Contains(plain) && plain.Any(char.IsLetter);
    }

    private static int LineOf(string text, int index) => text.AsSpan(0, index).Count('\n') + 1;

    /// <summary>
    /// Türkçe harf ya da Türkçe harf içermeyen sık arayüz sözcüklerinden biri ("Sessiz", "Kopyala", "pencere"…). Sözcük
    /// listesi bu kodda bulunan kaçakları kapsar; yeni bir kaçak bulunursa buraya eklenir.
    /// </summary>
    [GeneratedRegex(@"[çğıöşüÇĞİÖŞÜ]|\b(?:Sesli|asistan|Ses|Sessiz|mikrofon|Mikrofon|uygulama|Uygulama|daha|pencere|penceresini|dikkat|istiyor|dinliyor|Kopyala|Misafir|kipi|Duraklat|Oynat|Kilitle|Uyku|Kapat|Yeni|Ayarlar|Ayar|Dosya|Emin|misin|Geri|Pano|Saat|takvim|Pil|Bulutlu|Sisli|Ara|Hata|deyin|Tamam|Sil|Ekle|Kaydet|Anahtar|Yok|Bekle|Bilinmeyen|Kapat|Sabitle|Gizle|Bul|Aranan|ayarlar|kapat|yok|var|ile|veya|ya da)\b")]
    private static partial Regex TurkishLetter();

    [GeneratedRegex(@"(\$@|@\$|\$|@)?""(?:[^""\\]|\\.|"""")*""")]
    private static partial Regex StringLiteral();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline)]
    private static partial Regex XmlComment();

    [GeneratedRegex(@"\b(?:Text|Header|Content|ToolTip|Title|AutomationProperties\.Name|AutomationProperties\.HelpText)\s*=\s*""(?<value>[^""]*)""")]
    private static partial Regex XamlTextAttribute();

    [GeneratedRegex(@"<(?:TextBlock|Run|Label|Button|ToolTip|Hyperlink|Span|Bold|Italic|\w+:String)\b[^>]*>(?<value>[^<>]+)<")]
    private static partial Regex XamlElementText();

    [GeneratedRegex(@"&#x[0-9A-Fa-f]+;|&#\d+;|&\w+;")]
    private static partial Regex Entity();
}
