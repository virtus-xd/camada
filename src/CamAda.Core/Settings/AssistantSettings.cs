namespace CamAda.Core.Settings;

/// <summary>WhatsApp'ta hazır mesajla açılabilecek bir kişi (ayar dosyasına elle yazılır).</summary>
public sealed record WhatsAppContact(string Name, string Phone);

/// <summary>Sesli asistan Ada'nın ayarları (Plan 5). Aralık dışı değerler yüklenirken varsayılana döner.</summary>
public sealed record AssistantSettings
{
    public const string DefaultModel = "models/gemini-3.8-live";
    public const string DefaultLookupModel = "gemini-3.5-flash-lite";
    public const string DefaultVoice = "Kore";
    public const double DefaultWakeThreshold = 0.20;
    public const int DefaultSilenceSeconds = 8;
    public const int DefaultNoSpeechSeconds = 6;
    public const int DefaultMaxSessionMinutes = 10;
    public const string DefaultHotkey = "Win+Alt+A";

    /// <summary>Anahtar girilip asistan açılınca true; kapalıyken hiçbir şey dinlenmez.</summary>
    public bool Enabled { get; init; }

    public string Model { get; init; } = DefaultModel;

    /// <summary>Güncel bilgi araması için kullanılan model (Google araması bununla yapılır).</summary>
    public string LookupModel { get; init; } = DefaultLookupModel;

    public string Voice { get; init; } = DefaultVoice;

    /// <summary>Mikrofonun adından bir parça (ör. "Realtek"); null: Windows'un varsayılan iletişim mikrofonu.</summary>
    public string? Microphone { get; init; }

    public double WakeThreshold { get; init; } = DefaultWakeThreshold;

    public int SilenceSeconds { get; init; } = DefaultSilenceSeconds;

    public int NoSpeechSeconds { get; init; } = DefaultNoSpeechSeconds;

    public int MaxSessionMinutes { get; init; } = DefaultMaxSessionMinutes;

    public string Hotkey { get; init; } = DefaultHotkey;

    /// <summary>Tam ekran bir uygulama varken de "Hey Ada" dinlensin mi (varsayılan: hayır).</summary>
    public bool ListenInFullScreen { get; init; }

    /// <summary>Oturum boyunca öbür uygulamaların sesi kısılsın mı.</summary>
    public bool DuckOthers { get; init; } = true;

    public IReadOnlyList<WhatsAppContact> WhatsAppContacts { get; init; } = [];
}
