using CamAda.Core.Glass;
using CamAda.Core.Island;
using CamAda.Core.Record;
using CamAda.Core.Settings;

namespace CamAda.Core.Tests.Record;

public class RecordLayoutTests
{
    private static readonly RectD Screen = new(0, 0, 1536, 864);
    private static readonly RecordSettings Defaults = new();

    private static double Bottom(RectD r) => r.Y + r.Height;

    [Fact]
    public void Window_starts_at_the_left_edge_of_the_screen() =>
        Assert.Equal(0, RecordLayout.Compute(Screen, Defaults).Window.X);

    [Fact]
    public void Disc_is_45_percent_outside_the_screen()
    {
        var g = RecordLayout.Compute(Screen, Defaults);
        Assert.Equal(-144, g.Disc.X, 6);
        Assert.Equal(320, g.Disc.Width);
        Assert.Equal(320, g.Disc.Height);
        Assert.Equal(176, g.VisibleDisc.Width, 6);
        Assert.Equal(0, g.VisibleDisc.X);
    }

    [Fact]
    public void Disc_bottom_is_70_above_the_screen_bottom()
    {
        var g = RecordLayout.Compute(Screen, Defaults);
        Assert.Equal(864 - 70, g.Window.Y + Bottom(g.Disc), 6);
    }

    [Fact]
    public void Card_is_right_of_the_visible_disc_and_vertically_centered()
    {
        var g = RecordLayout.Compute(Screen, Defaults);
        Assert.Equal(176 + RecordLayout.CardGap, g.Card.X, 6);
        Assert.Equal(RecordLayout.CardWidth, g.Card.Width);
        Assert.Equal(g.Disc.Y + g.Disc.Height / 2, g.Card.Y + g.Card.Height / 2, 6);
    }

    // Ana ekranın solunda başka bir monitör varsa plağın ekran dışı kısmı orada görünmemeli.
    [Fact]
    public void Window_never_reaches_a_monitor_on_the_left()
    {
        var screen = new RectD(-1920, 0, 1920, 1080);
        var g = RecordLayout.Compute(screen, Defaults);
        Assert.Equal(screen.X, g.Window.X);
        Assert.True(g.Disc.X < 0);
    }

    [Fact]
    public void Window_stays_inside_the_screen_vertically()
    {
        var g = RecordLayout.Compute(Screen, Defaults with { BottomOffset = 0, Diameter = 640 });
        Assert.True(g.Window.Y >= Screen.Y);
        Assert.True(Bottom(g.Window) <= Bottom(Screen) + 1e-6);
        Assert.True(g.Window.Y + Bottom(g.Card) <= Bottom(Screen) + 1e-6);
    }

    [Fact]
    public void No_outside_fraction_puts_disc_at_the_window_edge() =>
        Assert.Equal(0, RecordLayout.Compute(Screen, Defaults with { OutsideFraction = 0 }).Disc.X);

    // --- RecordVisibility ---

    private static readonly RectD VisibleDiscOnScreen = new(0, 594, 220, 400);

    private static WindowSample Win(double x, double y, double w, double h, bool visible = true, bool cloaked = false,
        bool isDesktop = false, bool isOwn = false, bool isSelf = false) =>
        new(new RectD(x, y, w, h), visible, cloaked, Ignorable: false, HasTitle: true, isDesktop, isOwn, isSelf);

    // Son inceleme: Win+D masaüstünü (ve ona bağlı plağı) pencerelerin önüne getirir ama pencereler küçülmez; plağın
    // altında kalan büyütülmüş pencere "örtüyor" sayılıyor ve plak tam da masaüstü gösterilirken dönmüyordu.
    [Fact]
    public void Windows_below_the_record_do_not_cover() =>
        Assert.False(RecordVisibility.IsCovered(
            [Win(0, 0, 1920, 1080, isOwn: true, isSelf: true), Win(-8, -8, 1936, 1048)], VisibleDiscOnScreen));

    [Fact]
    public void Windows_above_the_record_cover() =>
        Assert.True(RecordVisibility.IsCovered(
            [Win(-8, -8, 1936, 1048), Win(0, 0, 1920, 1080, isOwn: true, isSelf: true)], VisibleDiscOnScreen));

    // --- DiscHit: tıklama ve üzerine gelme yalnız diskin dairesinde (dönen kare görüntünün köşelerinde değil) ---

    [Fact]
    public void Disc_center_and_edge_are_on_the_disc()
    {
        Assert.True(DiscHit.Contains(160, 160, 320, 320));
        Assert.True(DiscHit.Contains(160, 0.5, 320, 320));
        Assert.True(DiscHit.Contains(319.5, 160, 320, 320));
    }

    [Fact]
    public void Square_corners_are_off_the_disc()
    {
        Assert.False(DiscHit.Contains(5, 5, 320, 320));
        Assert.False(DiscHit.Contains(300, 300, 320, 320));
        Assert.False(DiscHit.Contains(-1, 160, 320, 320));
    }

    [Fact]
    public void Maximized_window_covers_the_record() =>
        Assert.True(RecordVisibility.IsCovered([Win(-8, -8, 1936, 1048)], VisibleDiscOnScreen));

    [Fact]
    public void Half_covering_window_does_not_hide_the_record() =>
        Assert.False(RecordVisibility.IsCovered([Win(100, 500, 800, 600)], VisibleDiscOnScreen));

    [Fact]
    public void Desktop_and_own_windows_do_not_cover() =>
        Assert.False(RecordVisibility.IsCovered(
            [Win(0, 0, 1920, 1080, isDesktop: true), Win(0, 0, 1920, 1080, isOwn: true)], VisibleDiscOnScreen));

    [Fact]
    public void Hidden_or_cloaked_windows_do_not_cover() =>
        Assert.False(RecordVisibility.IsCovered(
            [Win(0, 0, 1920, 1080, visible: false), Win(0, 0, 1920, 1080, cloaked: true)], VisibleDiscOnScreen));
}
