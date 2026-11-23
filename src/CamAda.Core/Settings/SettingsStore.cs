using System.Text.Json;
using System.Text.Json.Serialization;

namespace CamAda.Core.Settings;

/// <summary>Ayarları JSON dosyasından okur ve yazar. Bozuk dosyada çökmez, varsayılana döner.</summary>
public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly string _directory;

    public SettingsStore(string directory)
    {
        _directory = directory;
        FilePath = Path.Combine(directory, "settings.json");
    }

    public string FilePath { get; }

    private string CorruptBackupPath => Path.Combine(_directory, "settings.corrupt.json");

    /// <summary>Ayarları yükler. Dosya yoksa ya da bozuksa varsayılanları yazar ve döner.</summary>
    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        try
        {
            var json = File.ReadAllText(FilePath);
            return Sanitize(JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings());
        }
        catch (JsonException)
        {
            File.Copy(FilePath, CorruptBackupPath, overwrite: true);
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }
    }

    /// <summary>Elle düzenlenmiş, aralık dışı değerleri varsayılana çeker (ör. negatif gecikme zamanlayıcıyı çökertir).</summary>
    private static AppSettings Sanitize(AppSettings s)
    {
        var defaults = new AppSettings();
        return s with
        {
            HoverEnterDelayMs = s.HoverEnterDelayMs is >= 0 and <= 5000 ? s.HoverEnterDelayMs : defaults.HoverEnterDelayMs,
            HoverLeaveDelayMs = s.HoverLeaveDelayMs is >= 0 and <= 5000 ? s.HoverLeaveDelayMs : defaults.HoverLeaveDelayMs,
            StripHeight = s.StripHeight is > 0 and <= 200 ? s.StripHeight : defaults.StripHeight,
            GlassMode = Enum.IsDefined(s.GlassMode) ? s.GlassMode : defaults.GlassMode,
        };
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(FilePath, JsonSerializer.Serialize(settings, JsonOptions));
    }
}
