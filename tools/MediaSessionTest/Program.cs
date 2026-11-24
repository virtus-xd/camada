using System.Runtime.InteropServices;
using Windows.Graphics.Imaging;
using Windows.Media;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.Storage.Streams;

// Sahte "Spotify" medya oturumu: kimliği ve exe adı "spotify" içerdiği için CamAda onu Spotify sanır. Sessiz bir sesi döngüde
// çalar ve Windows'a üç parça gösterir: kırmızı kapaklı, mavi kapaklı ve kapaksız. Oynat/duraklat/ileri/geri düğmelerine
// yanıt verir; --seconds dolunca (varsayılan 600) kendiliğinden kapanır. Olaylar %TEMP%\spotify-deneme.log'a yazılır.
// Çalıştırma: dotnet run --project tools/MediaSessionTest -- [--no-cover] [--paused] [--seconds N] [--script "8:pause,14:play"]
SetCurrentProcessExplicitAppUserModelID("CamAda.SpotifyDeneme");

var noCover = args.Contains("--no-cover");
var startPaused = args.Contains("--paused");
var secondsIndex = Array.IndexOf(args, "--seconds");
var lifetime = TimeSpan.FromSeconds(secondsIndex >= 0 && secondsIndex + 1 < args.Length ? int.Parse(args[secondsIndex + 1]) : 600);
var log = Path.Combine(Path.GetTempPath(), "spotify-deneme.log");
void Log(string message) => File.AppendAllText(log, $"{DateTime.Now:HH:mm:ss.fff} {message}{Environment.NewLine}");

(string Title, uint? Color)[] tracks = [("Deneme Şarkı 1", 0xD02020), ("Deneme Şarkı 2", 0x2050D0), ("Deneme Şarkı 3", null)];
var trackDuration = TimeSpan.FromSeconds(200);
var index = noCover ? 2 : 0;
var trackStartedAt = DateTime.UtcNow;
var pausedAt = TimeSpan.Zero;
var playing = !startPaused;

var player = new MediaPlayer { IsLoopingEnabled = true, Volume = 0 };
player.CommandManager.IsEnabled = false;
var smtc = player.SystemMediaTransportControls;
smtc.IsEnabled = true;
smtc.IsPlayEnabled = smtc.IsPauseEnabled = smtc.IsNextEnabled = smtc.IsPreviousEnabled = true;
player.Source = MediaSource.CreateFromStream(SilentWav(TimeSpan.FromSeconds(30)), "audio/wav");

TimeSpan Position() => playing ? DateTime.UtcNow - trackStartedAt : pausedAt;

async Task ShowAsync()
{
    var (title, color) = tracks[index];
    var updater = smtc.DisplayUpdater;
    updater.Type = MediaPlaybackType.Music;
    updater.MusicProperties.Title = title;
    updater.MusicProperties.Artist = "CamAda";
    updater.MusicProperties.AlbumTitle = "Deneme";
    updater.Thumbnail = color is { } c ? RandomAccessStreamReference.CreateFromStream(await SolidPngAsync(c)) : null;
    updater.Update();
    UpdateTimeline();
    Log($"parça: {title} kapak={(color is null ? "yok" : $"#{color:X6}")}");
}

void UpdateTimeline() => smtc.UpdateTimelineProperties(new SystemMediaTransportControlsTimelineProperties
{
    StartTime = TimeSpan.Zero,
    EndTime = trackDuration,
    MinSeekTime = TimeSpan.Zero,
    MaxSeekTime = trackDuration,
    Position = Position(),
});

void SetPlaying(bool value)
{
    if (value == playing)
    {
        return;
    }

    if (value)
    {
        trackStartedAt = DateTime.UtcNow - pausedAt;
        player.Play();
    }
    else
    {
        pausedAt = Position();
        player.Pause();
    }

    playing = value;
    smtc.PlaybackStatus = value ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
    UpdateTimeline();
    Log(value ? "oynat" : "duraklat");
}

async Task SkipAsync(int step)
{
    index = (index + step + tracks.Length) % tracks.Length;
    trackStartedAt = DateTime.UtcNow;
    pausedAt = TimeSpan.Zero;
    await ShowAsync();
}

smtc.ButtonPressed += async (_, e) =>
{
    switch (e.Button)
    {
        case SystemMediaTransportControlsButton.Play: SetPlaying(true); break;
        case SystemMediaTransportControlsButton.Pause: SetPlaying(false); break;
        case SystemMediaTransportControlsButton.Next: await SkipAsync(1); break;
        case SystemMediaTransportControlsButton.Previous: await SkipAsync(-1); break;
    }
};

await ShowAsync();
if (playing)
{
    player.Play();
}

smtc.PlaybackStatus = playing ? MediaPlaybackStatus.Playing : MediaPlaybackStatus.Paused;
Log($"başladı (oynuyor={playing}, ömür={lifetime.TotalSeconds} sn)");

// --script "8:pause,14:play,20:next": belirtilen saniyelerde kendi kendine düğmeye basar (küresel medya tuşu kullanılmadan,
// kullanıcının gerçek oturumlarına dokunmadan deneme yapmak için).
var scriptIndex = Array.IndexOf(args, "--script");
var steps = (scriptIndex >= 0 && scriptIndex + 1 < args.Length ? args[scriptIndex + 1] : "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries)
    .Select(s => s.Split(':'))
    .Select(p => (At: DateTime.UtcNow + TimeSpan.FromSeconds(double.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture)), Action: p[1]))
    .ToList();

var deadline = DateTime.UtcNow + lifetime;
var lastTimeline = DateTime.UtcNow;
while (DateTime.UtcNow < deadline)
{
    await Task.Delay(TimeSpan.FromMilliseconds(250));
    foreach (var step in steps.Where(s => s.At <= DateTime.UtcNow).ToList())
    {
        steps.Remove(step);
        Log($"betik: {step.Action}");
        switch (step.Action)
        {
            case "pause": SetPlaying(false); break;
            case "play": SetPlaying(true); break;
            case "next": await SkipAsync(1); break;
            case "previous": await SkipAsync(-1); break;
            case "quit": deadline = DateTime.UtcNow; break;
        }
    }

    if (DateTime.UtcNow - lastTimeline > TimeSpan.FromSeconds(5))
    {
        lastTimeline = DateTime.UtcNow;
        UpdateTimeline();
    }
}

Log("kapandı");
player.Dispose();

static IRandomAccessStream SilentWav(TimeSpan length)
{
    const int sampleRate = 8000;
    var dataBytes = (int)(sampleRate * length.TotalSeconds) * 2;
    var wav = new MemoryStream();
    using (var w = new BinaryWriter(wav, System.Text.Encoding.ASCII, leaveOpen: true))
    {
        w.Write("RIFF"u8.ToArray());
        w.Write(36 + dataBytes);
        w.Write("WAVEfmt "u8.ToArray());
        w.Write(16);
        w.Write((short)1);          // PCM
        w.Write((short)1);          // mono
        w.Write(sampleRate);
        w.Write(sampleRate * 2);
        w.Write((short)2);
        w.Write((short)16);
        w.Write("data"u8.ToArray());
        w.Write(dataBytes);
        w.Write(new byte[dataBytes]);
    }

    wav.Position = 0;
    return wav.AsRandomAccessStream();
}

static async Task<IRandomAccessStream> SolidPngAsync(uint rgb)
{
    const int size = 300;
    var pixels = new byte[size * size * 4];
    for (var i = 0; i < pixels.Length; i += 4)
    {
        pixels[i] = (byte)rgb;              // B
        pixels[i + 1] = (byte)(rgb >> 8);   // G
        pixels[i + 2] = (byte)(rgb >> 16);  // R
        pixels[i + 3] = 255;
    }

    var stream = new InMemoryRandomAccessStream();
    var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
    encoder.SetPixelData(BitmapPixelFormat.Bgra8, BitmapAlphaMode.Premultiplied, size, size, 96, 96, pixels);
    await encoder.FlushAsync();
    stream.Seek(0);
    return stream;
}

[DllImport("shell32.dll", CharSet = CharSet.Unicode)]
static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
