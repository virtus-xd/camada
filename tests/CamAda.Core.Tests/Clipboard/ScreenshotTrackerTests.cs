using CamAda.Core.Clipboard;
using Microsoft.Extensions.Time.Testing;

namespace CamAda.Core.Tests.Clipboard;

public class ScreenshotTrackerTests
{
    private readonly FakeTimeProvider _time = new();

    private ScreenshotTracker Make() => new(_time);

    private void Ms(int ms) => _time.Advance(TimeSpan.FromMilliseconds(ms));

    [Fact]
    public void File_then_matching_clip_within_3s_drops_clip()
    {
        var tracker = Make();
        Assert.Null(tracker.OnFile(1280, 720));
        Ms(1000);

        Assert.Equal(ClipImageDecision.Drop, tracker.OnClipImage("p1", 1280, 720, fromScreenshotTool: true));
        Assert.False(tracker.HasPending);
    }

    [Fact]
    public void Tool_clip_waits_and_matching_file_claims_it()
    {
        var tracker = Make();
        Assert.Equal(ClipImageDecision.Hold, tracker.OnClipImage("p1", 1280, 720, fromScreenshotTool: true));
        Assert.True(tracker.HasPending);
        Ms(800);

        var claim = tracker.OnFile(1280, 720);

        Assert.Equal(new ScreenshotClaim("p1", InHistory: false), claim);
        Assert.False(tracker.HasPending);
        Ms(5000);
        Assert.Empty(tracker.TakeExpired());
    }

    [Fact]
    public void Tool_clip_without_file_expires_after_3s()
    {
        var tracker = Make();
        tracker.OnClipImage("p1", 1280, 720, fromScreenshotTool: true);

        Ms(2900);
        Assert.Empty(tracker.TakeExpired());
        Ms(200);
        Assert.Equal(["p1"], tracker.TakeExpired());
        Assert.False(tracker.HasPending);
        Assert.Empty(tracker.TakeExpired());
    }

    [Fact]
    public void Other_app_image_is_added_directly()
    {
        var tracker = Make();
        Assert.Equal(ClipImageDecision.AddToHistory, tracker.OnClipImage("p1", 1280, 720, fromScreenshotTool: false));
        Assert.False(tracker.HasPending);
    }

    [Fact]
    public void Clip_added_before_its_file_is_claimed_from_history()
    {
        // Pano sahibi tanınamazsa görsel önce geçmişe girer; dosyası gelince geçmişten çıkarılır (tek kayıt).
        var tracker = Make();
        Assert.Equal(ClipImageDecision.AddToHistory, tracker.OnClipImage("p1", 1920, 1080, fromScreenshotTool: false));
        tracker.AddedToHistory("item-7", 1920, 1080);
        Ms(1500);

        Assert.Equal(new ScreenshotClaim("item-7", InHistory: true), tracker.OnFile(1920, 1080));
        Assert.Null(tracker.OnFile(1920, 1080));
    }

    [Fact]
    public void Different_size_is_not_a_match()
    {
        var tracker = Make();
        tracker.OnFile(1280, 720);
        Assert.Equal(ClipImageDecision.AddToHistory, tracker.OnClipImage("p1", 600, 400, fromScreenshotTool: false));
        tracker.AddedToHistory("item-1", 600, 400);
        Assert.Null(tracker.OnFile(1280, 721));
    }

    [Fact]
    public void Old_file_does_not_match()
    {
        var tracker = Make();
        tracker.OnFile(1280, 720);
        Ms(4000);
        Assert.Equal(ClipImageDecision.AddToHistory, tracker.OnClipImage("p1", 1280, 720, fromScreenshotTool: false));
    }

    [Fact]
    public void Old_history_image_is_not_claimed()
    {
        var tracker = Make();
        tracker.OnClipImage("p1", 1280, 720, fromScreenshotTool: false);
        tracker.AddedToHistory("item-1", 1280, 720);
        Ms(4000);
        Assert.Null(tracker.OnFile(1280, 720));
    }
}
