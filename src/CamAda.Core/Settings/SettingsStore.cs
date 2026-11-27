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

    /// <summary>Dosya var ama okunamadı (ör. kilitli): bu oturumda üzerine yazılmaz, kullanıcının ayarları korunur.</summary>
    private bool _readFailed;

    public SettingsStore(string directory)
    {
        _directory = directory;
        FilePath = Path.Combine(directory, "settings.json");
    }

    public string FilePath { get; }

    private string CorruptBackupPath => Path.Combine(_directory, "settings.corrupt.json");

    /// <summary>
    /// Yalnız arayüz dilini okur ("tr", "en" ya da Otomatik için null); dosyaya hiç yazmaz, dönüştürmez, yedeklemez.
    /// Uygulama her kipte (bekçi, kurtarma dahil) olay döngüsü başlamadan çağırır; tam yükleme normal başlangıçta kalır.
    /// Öbür ayarlardan biri bozuk olsa da dil okunur.
    /// </summary>
    public string? ReadLanguage()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return null;
            }

            using var doc = JsonDocument.Parse(File.ReadAllText(FilePath));
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            foreach (var property in doc.RootElement.EnumerateObject())
            {
                if (string.Equals(property.Name, nameof(AppSettings.Language), StringComparison.OrdinalIgnoreCase))
                {
                    var value = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    return value is "tr" or "en" ? value : null;
                }
            }

            return null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    /// <summary>
    /// Ayarları yükler. Dosya yoksa ya da bozuksa varsayılanları yazar ve döner. Dosya okunamıyorsa (kilitli, izin yok)
    /// varsayılanlarla çalışılır ama dosyaya bu oturumda dokunulmaz.
    /// </summary>
    public AppSettings Load()
    {
        if (!File.Exists(FilePath))
        {
            var defaults = new AppSettings();
            Save(defaults);
            return defaults;
        }

        string json;
        try
        {
            json = File.ReadAllText(FilePath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _readFailed = true;
            return new AppSettings();
        }

        try
        {
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
            TryBackupCorrupt();
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
            PinnedApps = s.PinnedApps?.Where(p => p is not null && !string.IsNullOrWhiteSpace(p.LaunchPath))
                .Select(Repair)
                .ToList(),
            Weather = SanitizeWeather(s.Weather),
            Record = SanitizeRecord(s.Record),
            Assistant = SanitizeAssistant(s.Assistant),
            SpotifyClientId = string.IsNullOrWhiteSpace(s.SpotifyClientId) ? null : s.SpotifyClientId.Trim(),
            Language = s.Language is "tr" or "en" ? s.Language : null,
            Hotkeys = new HotkeySettings
            {
                MicToggle = Input.Hotkey.TryParse(s.Hotkeys?.MicToggle)?.ToString() ?? HotkeySettings.DefaultMicToggle,
                ClipboardOpen = Input.Hotkey.TryParse(s.Hotkeys?.ClipboardOpen)?.ToString() ?? HotkeySettings.DefaultClipboardOpen,
            },
        };
    }

    /// <summary>Aralık dışı plak ölçüleri varsayılana döner; etiket çapı aralık dışıysa varsayılan orana bırakılır.</summary>
    private static RecordSettings SanitizeRecord(RecordSettings? record)
    {
        var defaults = new RecordSettings();
        if (record is null)
        {
            return defaults;
        }

        var diameter = record.Diameter is >= 160 and <= 640 ? record.Diameter : defaults.Diameter;
        return record with
        {
            Diameter = diameter,
            OutsideFraction = record.OutsideFraction is >= 0 and <= 0.8 ? record.OutsideFraction : defaults.OutsideFraction,
            BottomOffset = record.BottomOffset is >= 0 and <= 600 ? record.BottomOffset : defaults.BottomOffset,
            LabelDiameter = record.LabelDiameter is { } label && label >= 40 && label <= diameter * 0.6 ? label : null,
        };
    }

    /// <summary>Ada'nın ayarları: aralık dışı sayılar, boş adlar ve geçersiz kısayol varsayılana döner; eksik kişiler atılır.</summary>
    private static AssistantSettings SanitizeAssistant(AssistantSettings? a)
    {
        var d = new AssistantSettings();
        if (a is null)
        {
            return d;
        }

        static string OrDefault(string? value, string fallback) => string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return a with
        {
            Model = OrDefault(a.Model, d.Model),
            LookupModel = OrDefault(a.LookupModel, d.LookupModel),
            Voice = OrDefault(a.Voice, d.Voice),
            Microphone = string.IsNullOrWhiteSpace(a.Microphone) ? null : a.Microphone.Trim(),
            WakeThreshold = a.WakeThreshold is >= 0.05 and <= 0.60 ? a.WakeThreshold : d.WakeThreshold,
            SilenceSeconds = a.SilenceSeconds is >= 3 and <= 60 ? a.SilenceSeconds : d.SilenceSeconds,
            NoSpeechSeconds = a.NoSpeechSeconds is >= 3 and <= 30 ? a.NoSpeechSeconds : d.NoSpeechSeconds,
            MaxSessionMinutes = a.MaxSessionMinutes is >= 1 and <= 14 ? a.MaxSessionMinutes : d.MaxSessionMinutes,
            Hotkey = Input.Hotkey.TryParse(a.Hotkey)?.ToString() ?? d.Hotkey,
            GuestModeMinutes = a.GuestModeMinutes is >= 1 and <= 30 ? a.GuestModeMinutes : d.GuestModeMinutes,
            SpeakerThreshold = a.SpeakerThreshold is >= 0.10 and <= 0.90 ? a.SpeakerThreshold : d.SpeakerThreshold,
            WhatsAppContacts = (a.WhatsAppContacts ?? [])
                .Where(c => c is not null && !string.IsNullOrWhiteSpace(c.Name) && !string.IsNullOrWhiteSpace(c.Phone))
                .Select(c => new WhatsAppContact(c.Name.Trim(), c.Phone.Trim()))
                .ToList(),
        };
    }

    /// <summary>Adı olmayan konum kapalı sayılır; aralık dışı ya da yarım koordinatlar silinir (ad yeniden aranır).</summary>
    private static WeatherLocation? SanitizeWeather(WeatherLocation? weather)
    {
        if (weather is null || string.IsNullOrWhiteSpace(weather.Name))
        {
            return null;
        }

        var valid = weather.Latitude is >= -90 and <= 90 && weather.Longitude is >= -180 and <= 180;
        return valid
            ? weather with { Name = weather.Name.Trim() }
            : new WeatherLocation(weather.Name.Trim(), null, null);
    }

    /// <summary>
    /// Elle eklenmiş sabitlenmiş uygulamada eksik alanları yoldan türetir (ör. yalnız "LaunchPath" yazılmış);
    /// yolu olmayan ya da boş girişler yukarıda elenir. Aksi hâlde null alanlar adayı her açılışta çökertir.
    /// </summary>
    private static Apps.PinnedApp Repair(Apps.PinnedApp app)
    {
        var processName = string.IsNullOrWhiteSpace(app.ProcessName)
            ? Path.GetFileName(app.LaunchPath).ToLowerInvariant()
            : app.ProcessName;
        return app with
        {
            ProcessName = processName,
            Id = string.IsNullOrWhiteSpace(app.Id) ? processName : app.Id,
            Name = string.IsNullOrWhiteSpace(app.Name) ? Path.GetFileNameWithoutExtension(app.LaunchPath) : app.Name,
            Arguments = app.Arguments ?? string.Empty,
        };
    }

    /// <summary>
    /// Ayarları geçici dosyaya yazıp yerine taşır (yarıda kalan yazım dosyayı bozmaz). Disk dolu, dosya kilitli ya da
    /// izin yoksa false; uygulama çökmez, değişiklik yalnız bu oturumda geçerli kalır.
    /// </summary>
    public bool Save(AppSettings settings)
    {
        if (_readFailed)
        {
            return false;
        }

        var temporary = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(_directory);
            File.WriteAllText(temporary, JsonSerializer.Serialize(settings, JsonOptions));
            File.Move(temporary, FilePath, overwrite: true);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                File.Delete(temporary);
            }
            catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
            {
            }

            return false;
        }
    }

    private void TryBackupCorrupt()
    {
        try
        {
            File.Copy(FilePath, CorruptBackupPath, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
