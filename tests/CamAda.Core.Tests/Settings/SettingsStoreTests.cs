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
        Assert.True(s.HideWindowsTaskbar);
        Assert.True(s.StartWithWindows);
        Assert.Equal(GlassMode.Wallpaper, s.GlassMode);
        Assert.Equal(56, s.StripHeight);
        Assert.Equal(120, s.HoverEnterDelayMs);
        Assert.Equal(350, s.HoverLeaveDelayMs);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
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
    public void Unknown_properties_ignored_and_missing_ones_default()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "GlassMode": "Solid", "Gelecek": 1 }""");
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(GlassMode.Solid, s.GlassMode);
        Assert.Equal(56, s.StripHeight);
    }
}
