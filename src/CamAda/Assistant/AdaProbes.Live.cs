#if DEBUG
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using CamAda.Core.Assistant;

namespace CamAda.Assistant;

internal static partial class AdaProbes
{
    /// <summary>
    /// İnternet takılması: yerel bir WebSocket sunucusu bağlantıyı kabul edip hiç okumaz (pong vermez). Ayarsız istemci
    /// asılı kalır; Ada'nın ayarıyla (ping/pong) istemci birkaç saniyede kopmayı görür. Google'a ve mikrofona dokunmaz.
    /// </summary>
    private static async Task StallAsync()
    {
        foreach (var keepAlive in new[] { false, true })
        {
            var port = 43900 + (keepAlive ? 1 : 0);
            using var listener = new HttpListener();
            listener.Prefixes.Add($"http://localhost:{port}/");
            listener.Start();
            var accepted = Task.Run(async () =>
            {
                var context = await listener.GetContextAsync();
                return (await context.AcceptWebSocketAsync(null)).WebSocket; // kabul et, sonra hiç okuma
            });

            using var client = new ClientWebSocket();
            if (keepAlive)
            {
                GeminiLiveClient.ConfigureKeepAlive(client.Options);
            }

            await client.ConnectAsync(new Uri($"ws://localhost:{port}/"), CancellationToken.None);
            using var server = await accepted;
            var started = DateTime.UtcNow;
            var receive = client.ReceiveAsync(new byte[1024], CancellationToken.None);
            var done = await Task.WhenAny(receive, Task.Delay(12000));
            var result = done == receive
                ? $"{(DateTime.UtcNow - started).TotalSeconds:F1} sn'de koptu ({receive.Exception?.InnerException?.GetType().Name ?? receive.Status.ToString()})"
                : "12 sn boyunca asılı kaldı";
            Log($"ada-takilma-sina: ping/pong {(keepAlive ? "açık" : "kapalı")}: {result}, istemci durumu {client.State}");
            client.Abort();
            listener.Stop();
        }
    }

    /// <summary>
    /// Ses göndermeden Google: kurulumdan sonra 15 sn boş bekleyip bağlantının ping/pong ile açık kaldığını denetler; sonra
    /// Ada'ya yazılı bir sistem notu gönderip sesli yanıt gelip gelmediğine bakar (10 dk vedası için; bir kısa yanıt ücreti).
    /// </summary>
    private static async Task TextTurnAsync()
    {
        var settings = Settings();
        if (new GeminiKeyStore(LocalDir).Load() is not { } key)
        {
            Log("ada-metin-sina: anahtar yok");
            return;
        }

        var setup = LiveSetup.Build(new LiveSetupOptions(settings.Model, settings.Voice,
            AdaInstruction.Build(DateTime.Now, [], System.Globalization.CultureInfo.CurrentUICulture), AdaTools.Declarations()));
        await using var client = await GeminiLiveClient.ConnectAsync(key, setup, CancellationToken.None);
        var audio = 0;
        var text = new StringBuilder();
        var turnDone = new TaskCompletionSource();
        AssistantError? closed = null;
        client.Attach(e =>
        {
            switch (e)
            {
                case LiveEvent.AudioChunk:
                    Interlocked.Increment(ref audio);
                    break;
                case LiveEvent.OutputText o:
                    lock (text)
                    {
                        text.Append(o.Text);
                    }

                    break;
                case LiveEvent.TurnComplete:
                    turnDone.TrySetResult();
                    break;
            }
        }, error => closed = error);

        await Task.Delay(15000);
        Log($"ada-metin-sina: 15 sn boş bekleme sonrası bağlantı açık={closed is null}");

        await client.SendTextAsync(AdaInstruction.MaxDurationGoodbye(System.Globalization.CultureInfo.CurrentUICulture));
        var finished = await Task.WhenAny(turnDone.Task, Task.Delay(12000)) == turnDone.Task;
        Log($"metin turu: yanıt bitti={finished}, ses parçası {audio}, yazı \"{text.ToString().Trim()}\", kapanış {closed?.ToString() ?? "yok"}");
        await client.CloseAsync();
    }
}
#endif
