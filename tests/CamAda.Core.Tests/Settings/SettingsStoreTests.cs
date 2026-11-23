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
        Assert.False(s.HideWindowsTaskbar);
        Assert.Equal(GlassMode.Wallpaper, s.GlassMode);
        Assert.Equal(56, s.StripHeight);
        Assert.Equal(120, s.HoverEnterDelayMs);
        Assert.Equal(350, s.HoverLeaveDelayMs);
        Assert.True(File.Exists(Path.Combine(_dir, "settings.json")));
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
    public void Unknown_properties_ignored_and_missing_ones_default()
    {
        File.WriteAllText(Path.Combine(_dir, "settings.json"), """{ "GlassMode": "Solid", "Gelecek": 1 }""");
        var s = new SettingsStore(_dir).Load();
        Assert.Equal(GlassMode.Solid, s.GlassMode);
        Assert.Equal(56, s.StripHeight);
    }
}
