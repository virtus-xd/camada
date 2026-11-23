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
            var settings = Sanitize(JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings());
            if (FileVersion(json) < AppSettings.CurrentVersion)
            {
                settings = Migrate(settings);
                Save(settings);
            }

            return settings;
        }
        catch (JsonException)
        {
            File.Copy(FilePath, CorruptBackupPath, overwrite: true);
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }
    }

    /// <summary>
    /// Dosyadaki sürüm. Kayıt türünün varsayılanı 2 olduğundan özelliğin dosyada olup olmadığına JSON'dan bakılır;
    /// yoksa dosya Plan 2 öncesidir (sürüm 1).
    /// </summary>
    private static int FileVersion(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object)
        {
            return 1;
        }

        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (string.Equals(property.Name, nameof(AppSettings.SettingsVersion), StringComparison.OrdinalIgnoreCase)
                && property.Value.TryGetInt32(out var version))
            {
                return version;
            }
        }

        return 1;
    }

    /// <summary>
    /// Sürüm 1 → 2: Plan 1'de görev çubuğu deneme için varsayılan görünürdü; ada artık onun yerini aldığından bir kereye
    /// mahsus gizlenir. Kullanıcı sonra menüden geri açarsa sürüm 2 dosyada kalır ve seçimi korunur.
    /// </summary>
    private static AppSettings Migrate(AppSettings settings) =>
        settings with { HideWindowsTaskbar = true, SettingsVersion = AppSettings.CurrentVersion };

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
