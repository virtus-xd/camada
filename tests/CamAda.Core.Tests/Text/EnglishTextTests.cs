using CamAda.Core.Bluetooth;
using CamAda.Core.Calendar;
using CamAda.Core.Clipboard;
using CamAda.Core.Network;
using CamAda.Core.Power;
using CamAda.Core.Search;
using CamAda.Core.Time;
using CamAda.Core.Weather;
using static CamAda.Core.Tests.Text.Cultures;

namespace CamAda.Core.Tests.Text;

/// <summary>Çekirdekte metin üreten fonksiyonların İngilizce karşılıkları (Türkçeleri kendi test dosyalarında).</summary>
public class EnglishTextTests
{
    private static readonly DateTime Friday = new(2026, 11, 27, 14, 12, 0);

    [Fact]
    public void Clock_is_24h_in_turkish_and_follows_the_region_in_english()
    {
        Assert.Equal("14:12", ClockFormatter.Time(Friday, Tr, En)); // kullanıcının kendi durumu: Türkçe arayüz, ABD bölgesi
        Assert.Equal("2:12 PM", ClockFormatter.Time(Friday, En, En));
        Assert.Equal("14:12", ClockFormatter.Time(Friday, En, EnGb));
    }

    [Fact]
    public void Island_date_uses_the_ui_language()
    {
        Assert.Equal("Fri, November 27", ClockFormatter.Date(Friday, En));
        Assert.Equal("Cum, 27 Kasım", ClockFormatter.Date(Friday, Tr));
    }

    [Fact]
    public void Battery_texts()
    {
        var threeTen = new TimeSpan(3, 10, 0);
        Assert.Equal("Charging · 80%", BatteryText.Header(new BatterySnapshot(80, BatteryCharge.Charging, true, null), En));
        Assert.Equal("Fully charged", BatteryText.Header(new BatterySnapshot(100, BatteryCharge.Idle, true, null), En));
        Assert.Equal("Plugged in · 80%", BatteryText.Header(new BatterySnapshot(80, BatteryCharge.Idle, true, null), En));
        Assert.Equal("76% · 3 h 10 min", BatteryText.Header(new BatterySnapshot(76, BatteryCharge.Discharging, false, threeTen), En));
        Assert.Equal("Battery 76%, 3 h 10 min left", BatteryText.AccessibleName(new BatterySnapshot(76, BatteryCharge.Discharging, false, threeTen), En));
        Assert.Equal("Battery 76%, charging", BatteryText.AccessibleName(new BatterySnapshot(76, BatteryCharge.Charging, true, null), En));
        Assert.Equal("Battery 100%, fully charged", BatteryText.AccessibleName(new BatterySnapshot(100, BatteryCharge.Idle, true, null), En));
        Assert.Equal("Battery 80%, plugged in", BatteryText.AccessibleName(new BatterySnapshot(80, BatteryCharge.Idle, true, null), En));
        Assert.Equal("No battery", BatteryText.AccessibleName(new BatterySnapshot(0, BatteryCharge.NotPresent, false, null), En));
        Assert.Equal("18%", BatteryText.CompactText(new BatterySnapshot(18, BatteryCharge.Discharging, false, null), En));
        Assert.Equal("45 min", BatteryText.FormatRemaining(TimeSpan.FromMinutes(45), En));
        Assert.Equal("3 h", BatteryText.FormatRemaining(TimeSpan.FromHours(3), En));
    }

    [Fact]
    public void Clipboard_ages()
    {
        var offset = TimeSpan.FromHours(3);
        var now = new DateTimeOffset(2026, 11, 24, 15, 0, 0, offset);
        Assert.Equal("now", ClipAge.Format(now.AddSeconds(-30), now, En));
        Assert.Equal("2 min", ClipAge.Format(now.AddMinutes(-2), now, En));
        Assert.Equal("3 h", ClipAge.Format(now.AddHours(-3), now, En));
        Assert.Equal("yesterday", ClipAge.Format(new DateTimeOffset(2026, 11, 23, 23, 0, 0, offset), now, En));
        Assert.Equal("Nov 12", ClipAge.Format(new DateTimeOffset(2026, 11, 12, 10, 0, 0, offset), now, En));
    }

    [Fact]
    public void Calendar_title_and_weekdays()
    {
        Assert.Equal("November 2026", MonthGrid.Title(2026, 11, En));
        Assert.Equal(["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"], MonthGrid.WeekdayHeaders(En));
    }

    [Fact]
    public void Network_texts()
    {
        Assert.Equal("Wi‑Fi: Home, internet access", NetworkIcons.AccessibleName(Snap(NetworkLink.Wifi, NetworkReach.Internet, ssid: "Home"), En));
        Assert.Equal("Wi‑Fi: connected, no internet", NetworkIcons.AccessibleName(Snap(NetworkLink.Wifi, NetworkReach.Local), En));
        Assert.Equal("Ethernet, internet access", NetworkIcons.AccessibleName(Snap(NetworkLink.Ethernet, NetworkReach.Internet), En));
        Assert.Equal("Wi‑Fi off", NetworkIcons.AccessibleName(Snap(NetworkLink.None, NetworkReach.None, radioOn: false), En));
        Assert.Equal("Wi‑Fi: not connected", NetworkIcons.AccessibleName(Snap(NetworkLink.None, NetworkReach.None), En));
        Assert.Equal("No network connection", NetworkIcons.AccessibleName(Snap(NetworkLink.None, NetworkReach.None, adapter: false), En));
        Assert.Equal("Connected", NetworkIcons.WifiTileSubtitle(Snap(NetworkLink.Wifi, NetworkReach.Internet), En));
        Assert.Equal("Not connected", NetworkIcons.WifiTileSubtitle(Snap(NetworkLink.Ethernet, NetworkReach.Internet), En));
        Assert.Equal("Off", NetworkIcons.WifiTileSubtitle(Snap(NetworkLink.Ethernet, NetworkReach.Internet, radioOn: false), En));
        Assert.Equal("Unavailable", NetworkIcons.WifiTileSubtitle(Snap(NetworkLink.Ethernet, NetworkReach.Internet, adapter: false), En));
    }

    [Fact]
    public void Bluetooth_tile()
    {
        Assert.Equal("Unavailable", BluetoothKinds.TileSubtitle(false, false, [], En));
        Assert.Equal("Off", BluetoothKinds.TileSubtitle(true, false, [], En));
        Assert.Equal("On", BluetoothKinds.TileSubtitle(true, true, [], En));
        Assert.Equal("Buds2 connected", BluetoothKinds.TileSubtitle(true, true, ["Buds2"], En));
        Assert.Equal("2 devices connected", BluetoothKinds.TileSubtitle(true, true, ["Buds2", "Mouse"], En));
    }

    [Theory]
    [InlineData(0, "Clear")]
    [InlineData(2, "Partly cloudy")]
    [InlineData(3, "Cloudy")]
    [InlineData(45, "Foggy")]
    [InlineData(53, "Drizzle")]
    [InlineData(63, "Rainy")]
    [InlineData(73, "Snowy")]
    [InlineData(81, "Showers")]
    [InlineData(85, "Snow showers")]
    [InlineData(95, "Thunderstorm")]
    [InlineData(999, "")]
    public void Weather_descriptions(int code, string expected) => Assert.Equal(expected, WeatherText.Describe(code, En));

    [Fact]
    public void Web_search_row_is_in_the_ui_language()
    {
        var items = new[] { new SearchItem("Steam", SearchKind.App, "steam.exe") };
        Assert.Equal("Search the web: “ste”", SearchRanker.Rank("ste", items, En).Last().Name);
        Assert.Equal("Web'de ara: “ste”", SearchRanker.Rank("ste", items, Tr).Last().Name);
    }

    [Fact]
    public void Settings_pages_show_the_ui_language_and_match_both()
    {
        var english = SettingsPages.For(En);
        var turkish = SettingsPages.For(Tr);
        Assert.Contains(english, p => p.Name == "Sound settings" && p.Target == "ms-settings:sound");
        Assert.Contains(turkish, p => p.Name == "Ses ayarları" && p.Target == "ms-settings:sound");
        Assert.Equal("Settings", english[^1].Name);
        Assert.Equal("Ayarlar", turkish[^1].Name);
        Assert.Equal("Sound settings", SearchRanker.Rank("ses", english, En)[0].Name); // Türkçe yazılan İngilizce arayüzde de bulunur
        Assert.Equal("Ses ayarları", SearchRanker.Rank("sound", turkish, Tr)[0].Name);
    }

    [Fact]
    public void Ada_listening_line_and_voice_sentences()
    {
        Assert.Equal("Listening…", CamAda.Core.Assistant.AdaText.ListeningLine(guest: false, En));
        Assert.Equal("Say “Ada”, I'm listening", CamAda.Core.Assistant.AdaText.ListeningLine(guest: true, En));
        var english = CamAda.Core.Assistant.VoiceEnrollmentText.Sentences(En);
        var turkish = CamAda.Core.Assistant.VoiceEnrollmentText.Sentences(Tr);
        Assert.Equal(5, english.Count);
        Assert.Equal(turkish.Count, english.Count);
        Assert.StartsWith("Ada,", english[1]); // ikinci cümle Ada'ya seslenir (misafir kipi bu cümleye dayanır)
        Assert.StartsWith("Ada,", turkish[1]);
    }

    [Fact]
    public void Screenshot_file_name() =>
        Assert.Equal("Screenshot 2026-11-26 021530.png", ScreenshotName.For(new DateTime(2026, 11, 26, 2, 15, 30), En));

    private static NetworkSnapshot Snap(NetworkLink link, NetworkReach reach, string? ssid = null, bool radioOn = true, bool adapter = true) =>
        new(link, reach, 4, adapter, radioOn, ssid);
}
