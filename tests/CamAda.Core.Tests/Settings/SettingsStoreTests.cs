using CamAda.Core.Settings;

namespace CamAda.Core.Tests.Settings;

public class SettingsStoreTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("camada-").FullName;
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void Missing_file_returns_defaults_and_writes_file()
    {
        var store = new SettingsStore(_dir);
        var s = store.Load();
        Assert.False(s.HideWindowsTaskbar); // yeni kullanıcı onaylamadan görev çubuğu gizlenmez
        Assert.True(s.StartWithWindows);
        Assert.Equal(GlassMode.Wallpaper, s.GlassMode);
        Assert.Equal(56, s.StripHeight);
        Assert.Equal(120, s.HoverEnterDelayMs);
        Assert.Equal(350, s.HoverLeaveDelayMs);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
    }

    [Fact]
    public void Spotify_client_id_is_trimmed()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "SettingsVersion": 2, "SpotifyClientId": "  abc123  " }""");
        Assert.Equal("abc123", new SettingsStore(_dir).Load().SpotifyClientId);
    }

    [Fact]
    public void Blank_spotify_client_id_is_null()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "SettingsVersion": 2, "SpotifyClientId": "   " }""");
        Assert.Null(new SettingsStore(_dir).Load().SpotifyClientId);
    }

    [Fact]
    public void Record_has_spec_defaults()
    {
        var r = new SettingsStore(_dir).Load().Record;
        Assert.True(r.Enabled);
        Assert.Equal(320, r.Diameter);
        Assert.Equal(0.45, r.OutsideFraction);
        Assert.Equal(70, r.BottomOffset);
        Assert.Null(r.LabelDiameter);
        Assert.Equal(108, r.EffectiveLabelDiameter);
    }

    [Fact]
    public void Out_of_range_record_values_fall_back_to_defaults()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"),
            """{ "SettingsVersion": 2, "Record": { "Diameter": 0, "OutsideFraction": 1.5, "BottomOffset": -5, "LabelDiameter": 500 } }""");
        var r = new SettingsStore(_dir).Load().Record;
        Assert.Equal(320, r.Diameter);
        Assert.Equal(0.45, r.OutsideFraction);
        Assert.Equal(70, r.BottomOffset);
        Assert.Null(r.LabelDiameter);
    }

    [Theory]
    [InlineData("""{ "SettingsVersion": 2 }""", "Ctrl+Shift+M")]
    [InlineData("""{ "SettingsVersion": 2, "Hotkeys": { "MicToggle": "Foo" } }""", "Ctrl+Shift+M")]
    [InlineData("""{ "SettingsVersion": 2, "Hotkeys": null }""", "Ctrl+Shift+M")]
    [InlineData("""{ "SettingsVersion": 2, "Hotkeys": { "MicToggle": "Alt+F13" } }""", "Alt+F13")]
    public void Hotkeys_default_and_invalid_falls_back(string json, string expected)
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), json);
        Assert.Equal(expected, new SettingsStore(_dir).Load().Hotkeys.MicToggle);
    }

    [Theory]
    [InlineData("""{ "SettingsVersion": 2 }""", "Win+Alt+V")]
    [InlineData("""{ "SettingsVersion": 2, "Hotkeys": { "ClipboardOpen": "Foo" } }""", "Win+Alt+V")]
    [InlineData("""{ "SettingsVersion": 2, "Hotkeys": null }""", "Win+Alt+V")]
    [InlineData("""{ "SettingsVersion": 2, "Hotkeys": { "ClipboardOpen": "Ctrl+Shift+V" } }""", "Ctrl+Shift+V")]
    public void Clipboard_hotkey_default_and_invalid_falls_back(string json, string expected)
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), json);
        Assert.Equal(expected, new SettingsStore(_dir).Load().Hotkeys.ClipboardOpen);
    }

    [Fact]
    public void Hidden_record_survives_save_and_load()
    {
        var store = new SettingsStore(_dir);
        var s = store.Load();
        store.Save(s with { Record = s.Record with { Enabled = false, Diameter = 400 } });

        var r = new SettingsStore(_dir).Load().Record;
        Assert.False(r.Enabled);
        Assert.Equal(400, r.Diameter); // gizlemek diğer plak ayarlarını sıfırlamaz
    }

    [Fact]
    public void Null_record_falls_back_to_defaults()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "SettingsVersion": 2, "Record": null }""");
        Assert.Equal(320, new SettingsStore(_dir).Load().Record.Diameter);
    }

    [Fact]
    public void Effective_label_diameter_is_not_written_to_the_file()
    {
        var store = new SettingsStore(_dir);
        store.Load();
        Assert.DoesNotContain("EffectiveLabelDiameter", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Weather_is_off_by_default()
    {
        var s = new SettingsStore(_dir).Load();
        Assert.Null(s.Weather);
    }

    [Fact]
    public void Weather_city_without_coordinates_is_kept()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "SettingsVersion": 2, "Weather": { "Name": "İstanbul" } }""");
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(new WeatherLocation("İstanbul", null, null), s.Weather);
    }

    [Fact]
    public void Out_of_range_weather_coordinates_are_dropped()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"),
            """{ "SettingsVersion": 2, "Weather": { "Name": "Ankara", "Latitude": 95, "Longitude": 32.4 } }""");
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(new WeatherLocation("Ankara", null, null), s.Weather);
    }

    [Fact]
    public void Weather_without_name_is_off()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "SettingsVersion": 2, "Weather": { "Name": "  " } }""");
        Assert.Null(new SettingsStore(_dir).Load().Weather);
    }

    [Fact]
    public void Old_file_without_version_turns_on_taskbar_hiding_once()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "HideWindowsTaskbar": false }""");
        var store = new SettingsStore(_dir);

        var s = store.Load();

        Assert.True(s.HideWindowsTaskbar);
        Assert.Equal(2, s.SettingsVersion);
        Assert.Contains("\"SettingsVersion\": 2", File.ReadAllText(store.FilePath));
    }

    [Fact]
    public void Versioned_file_keeps_users_choice()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "HideWindowsTaskbar": false, "SettingsVersion": 2 }""");
        var store = new SettingsStore(_dir);

        Assert.False(store.Load().HideWindowsTaskbar);
    }

    [Fact]
    public void Hand_edited_pinned_entries_are_repaired_or_dropped()
    {
        // Elle düzenlenmiş dosya: yanlış anahtarlı giriş, null giriş ve eksik alanlı giriş.
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """
            {
              "SettingsVersion": 2,
              "PinnedApps": [
                { "Id": "x", "Name": "X", "Path": "C:\\x.exe" },
                null,
                { "LaunchPath": "C:\\Oyun\\Oyun.exe" }
              ]
            }
            """);

        var pinned = new SettingsStore(_dir).Load().PinnedApps;

        var app = Assert.Single(pinned!);
        Assert.Equal(@"C:\Oyun\Oyun.exe", app.LaunchPath);
        Assert.Equal("oyun.exe", app.ProcessName);
        Assert.Equal("oyun.exe", app.Id);
        Assert.Equal("Oyun", app.Name);
        Assert.Equal("", app.Arguments);
    }

    [Fact]
    public void Save_then_load_round_trips()
    {
        var store = new SettingsStore(_dir);
        store.Save(new AppSettings { HideWindowsTaskbar = true, GlassMode = GlassMode.Acrylic });
        var s = store.Load();
        Assert.True(s.HideWindowsTaskbar);
        Assert.Equal(GlassMode.Acrylic, s.GlassMode);
    }

    [Fact]
    public void Corrupt_file_returns_defaults_and_keeps_backup()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), "{ bozuk");
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(new AppSettings(), s);
        Assert.Equal("{ bozuk", File.ReadAllText(Path.Combine(_dir, "settings.corrupt.json")));
    }

    [Fact]
    public void Out_of_range_hand_edited_values_fall_back_to_defaults()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"),
            """{ "HoverEnterDelayMs": -5, "HoverLeaveDelayMs": 999999, "StripHeight": -10, "GlassMode": "Solid" }""");
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(120, s.HoverEnterDelayMs);
        Assert.Equal(350, s.HoverLeaveDelayMs);
        Assert.Equal(56, s.StripHeight);
        Assert.Equal(GlassMode.Solid, s.GlassMode); // geçerli değerler korunur
    }

    [Fact]
    public void Unknown_enum_value_falls_back_to_defaults_without_crashing()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "GlassMode": "Gökkuşağı" }""");
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(GlassMode.Wallpaper, s.GlassMode);
    }

    [Fact]
    public void Missing_pinned_apps_stay_null()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "GlassMode": "Solid" }""");
        Assert.Null(new SettingsStore(_dir).Load().PinnedApps);
    }

    [Fact]
    public void Pinned_apps_round_trip()
    {
        var store = new SettingsStore(_dir);
        var pins = new[] { new CamAda.Core.Apps.PinnedApp("discord", "Discord", @"C:\D\Update.exe", "--processStart Discord.exe", "discord.exe") };
        store.Save(new AppSettings { PinnedApps = pins });
        var loaded = store.Load().PinnedApps;
        Assert.NotNull(loaded);
        Assert.Equal(pins[0], Assert.Single(loaded));
    }

    [Fact]
    public void Save_reports_failure_instead_of_throwing_when_folder_is_unusable()
    {
        var blocked = Path.Combine(_dir, "dosya");
        File.WriteAllText(blocked, "klasör değil");
        Assert.False(new SettingsStore(blocked).Save(new AppSettings()));
    }

    [Fact]
    public void Locked_file_loads_defaults_and_is_never_overwritten_this_session()
    {
        var path = Path.Combine(_dir, "settings.json");
        const string original = """{ "GlassMode": "Solid", "SettingsVersion": 2 }""";
        File.WriteAllText(path, original);
        var store = new SettingsStore(_dir);

        AppSettings loaded;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            loaded = store.Load();
        }

        Assert.Equal(new AppSettings(), loaded);
        Assert.False(store.Save(loaded with { GlassMode = GlassMode.Acrylic }));
        Assert.Equal(original, File.ReadAllText(path));
    }

    [Fact]
    public void Save_replaces_the_file_without_leaving_temporary_files()
    {
        var store = new SettingsStore(_dir);
        Assert.True(store.Save(new AppSettings { GlassMode = GlassMode.Solid }));
        Assert.True(store.Save(new AppSettings { GlassMode = GlassMode.Acrylic }));
        Assert.Equal(GlassMode.Acrylic, store.Load().GlassMode);
        Assert.Equal(["settings.json"], Directory.GetFiles(_dir).Select(Path.GetFileName));
    }

    [Fact]
    public void Assistant_has_spec_defaults()
    {
        var a = new SettingsStore(_dir).Load().Assistant;
        Assert.False(a.Enabled);
        Assert.Equal("models/gemini-3.8-live", a.Model);
        Assert.Equal("gemini-3.5-flash-lite", a.LookupModel);
        Assert.Equal("Kore", a.Voice);
        Assert.Null(a.Microphone);
        Assert.Equal(0.20, a.WakeThreshold);
        Assert.Equal(8, a.SilenceSeconds);
        Assert.Equal(6, a.NoSpeechSeconds);
        Assert.Equal(10, a.MaxSessionMinutes);
        Assert.Equal("Win+Alt+A", a.Hotkey);
        Assert.False(a.ListenInFullScreen);
        Assert.True(a.DuckOthers);
        Assert.Empty(a.WhatsAppContacts);
        Assert.Equal(2, a.GuestModeMinutes);
        Assert.Equal(0.33, a.SpeakerThreshold);
    }

    [Theory]
    [InlineData(0, 0.05)]
    [InlineData(31, 0.95)]
    public void Out_of_range_guest_values_fall_back(int minutes, double threshold)
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"),
            $$"""{ "Assistant": { "GuestModeMinutes": {{minutes}}, "SpeakerThreshold": {{threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } }""");
        var a = new SettingsStore(_dir).Load().Assistant;
        Assert.Equal(2, a.GuestModeMinutes);
        Assert.Equal(0.33, a.SpeakerThreshold);
    }

    [Fact]
    public void In_range_guest_values_are_kept()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "Assistant": { "GuestModeMinutes": 30, "SpeakerThreshold": 0.1 } }""");
        var a = new SettingsStore(_dir).Load().Assistant;
        Assert.Equal(30, a.GuestModeMinutes);
        Assert.Equal(0.1, a.SpeakerThreshold);
    }

    [Theory]
    [InlineData(0.01)]
    [InlineData(0.9)]
    public void Out_of_range_wake_threshold_falls_back(double threshold)
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"),
            $$"""{ "Assistant": { "WakeThreshold": {{threshold.ToString(System.Globalization.CultureInfo.InvariantCulture)}} } }""");
        Assert.Equal(0.20, new SettingsStore(_dir).Load().Assistant.WakeThreshold);
    }

    [Fact]
    public void Out_of_range_assistant_values_fall_back()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"),
            """
            { "Assistant": { "SilenceSeconds": 0, "NoSpeechSeconds": 99, "MaxSessionMinutes": 30, "Hotkey": "Ctrl+",
                             "Voice": "", "Model": " ", "LookupModel": "", "Microphone": "  " } }
            """);
        var a = new SettingsStore(_dir).Load().Assistant;
        Assert.Equal(8, a.SilenceSeconds);
        Assert.Equal(6, a.NoSpeechSeconds);
        Assert.Equal(10, a.MaxSessionMinutes);
        Assert.Equal("Win+Alt+A", a.Hotkey);
        Assert.Equal("Kore", a.Voice);
        Assert.Equal("models/gemini-3.8-live", a.Model);
        Assert.Equal("gemini-3.5-flash-lite", a.LookupModel);
        Assert.Null(a.Microphone);
    }

    [Fact]
    public void Valid_assistant_values_are_kept()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"),
            """{ "Assistant": { "Enabled": true, "WakeThreshold": 0.3, "Microphone": " Realtek ", "SilenceSeconds": 12 } }""");
        var a = new SettingsStore(_dir).Load().Assistant;
        Assert.True(a.Enabled);
        Assert.Equal(0.3, a.WakeThreshold);
        Assert.Equal("Realtek", a.Microphone);
        Assert.Equal(12, a.SilenceSeconds);
    }

    [Fact]
    public void Contacts_without_name_or_phone_are_dropped()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"),
            """
            { "Assistant": { "WhatsAppContacts": [ { "Name": "Ahmet", "Phone": "0555 111 22 33" }, { "Name": "", "Phone": "1" },
                                                   { "Name": "X", "Phone": " " }, null ] } }
            """);
        var contact = Assert.Single(new SettingsStore(_dir).Load().Assistant.WhatsAppContacts);
        Assert.Equal(new WhatsAppContact("Ahmet", "0555 111 22 33"), contact);
    }

    [Fact]
    public void Unknown_properties_ignored_and_missing_ones_default()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "GlassMode": "Solid", "Gelecek": 1 }""");
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(GlassMode.Solid, s.GlassMode);
        Assert.Equal(56, s.StripHeight);
    }

    // ReadLanguage: uygulama olay döngüsü başlamadan her kipte çağrılır; dosyaya asla yazmaz.

    [Fact]
    public void ReadLanguage_without_file_is_automatic_and_creates_nothing()
    {
        Assert.Null(new SettingsStore(_dir).ReadLanguage());
        Assert.Empty(Directory.GetFiles(_dir));
    }

    [Theory]
    [InlineData("en")]
    [InlineData("tr")]
    public void ReadLanguage_returns_the_saved_language(string language)
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), $$"""{ "SettingsVersion": 2, "Language": "{{language}}" }""");
        Assert.Equal(language, new SettingsStore(_dir).ReadLanguage());
    }

    [Theory]
    [InlineData("""{ "SettingsVersion": 2, "Language": "de" }""")]
    [InlineData("""{ "SettingsVersion": 2, "Language": 5 }""")]
    [InlineData("""{ "SettingsVersion": 2 }""")]
    [InlineData("""[ "en" ]""")]
    public void ReadLanguage_treats_unknown_values_as_automatic(string json)
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), json);
        Assert.Null(new SettingsStore(_dir).ReadLanguage());
    }

    [Fact]
    public void ReadLanguage_ignores_property_name_case_like_load()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "SettingsVersion": 2, "language": "en" }""");
        Assert.Equal("en", new SettingsStore(_dir).ReadLanguage());
    }

    [Fact]
    public void ReadLanguage_survives_an_unrelated_bad_setting()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "SettingsVersion": 2, "GlassMode": "Yok", "Language": "en" }""");
        Assert.Equal("en", new SettingsStore(_dir).ReadLanguage());
    }

    [Fact]
    public void ReadLanguage_on_corrupt_file_is_automatic_and_leaves_it_alone()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, "{ bozuk");
        Assert.Null(new SettingsStore(_dir).ReadLanguage());
        Assert.Equal("{ bozuk", File.ReadAllText(path));
        Assert.Single(Directory.GetFiles(_dir)); // yedek dosyası da yazılmaz
    }

    [Fact]
    public void ReadLanguage_does_not_migrate_an_old_file()
    {
        var path = Path.Combine(_dir, "settings.json");
        const string old = """{ "Language": "en" }""";
        File.WriteAllText(path, old);
        Assert.Equal("en", new SettingsStore(_dir).ReadLanguage());
        Assert.Equal(old, File.ReadAllText(path));
    }

    [Fact]
    public void ReadLanguage_on_locked_file_is_automatic()
    {
        var path = Path.Combine(_dir, "settings.json");
        File.WriteAllText(path, """{ "SettingsVersion": 2, "Language": "en" }""");
        using var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Null(new SettingsStore(_dir).ReadLanguage());
    }
}
