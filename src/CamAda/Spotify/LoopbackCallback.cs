using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CamAda.Core.Spotify;

namespace CamAda.Spotify;

/// <summary>
/// Spotify girişinden sonra tarayıcının yönlendirdiği http://127.0.0.1:43821/callback isteğini yakalar. HttpListener yerine
/// TcpListener: 127.0.0.1 için yönetici izni (URL ACL) gerekmez. Bağlantılar ayrı ayrı okunur; tarayıcıların önceden açıp
/// boş bıraktığı bağlantılar asıl isteği bekletmez. /callback isteğine Türkçe kısa bir sayfa döner, başka yollar 404 alır.
/// </summary>
public static class LoopbackCallback
{
    /// <summary>Geri dönüşü bekler; zaman aşımında, iptalde ya da port meşgulse null.</summary>
    public static async Task<OAuthCallbackResult?> WaitAsync(TimeSpan timeout, CancellationToken cancellation)
    {
        TcpListener listener;
        try
        {
            listener = new TcpListener(IPAddress.Loopback, SpotifyAuth.CallbackPort);
            listener.Start();
        }
        catch (SocketException ex)
        {
            Debug.WriteLine($"Spotify geri dönüş portu açılamadı: {ex.Message}");
            return null;
        }

        using var stop = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        stop.CancelAfter(timeout);
        var result = new TaskCompletionSource<OAuthCallbackResult?>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var registration = stop.Token.Register(() => result.TrySetResult(null));
        var accepting = AcceptAsync(listener, result, stop.Token);
        try
        {
            return await result.Task;
        }
        finally
        {
            stop.Cancel();
            listener.Stop();
            await accepting;
        }
    }

    private static async Task AcceptAsync(TcpListener listener, TaskCompletionSource<OAuthCallbackResult?> result, CancellationToken stop)
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stop);
                _ = HandleAsync(client, result, stop);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // dinleyici kapandı
        }
    }

    private static async Task HandleAsync(TcpClient client, TaskCompletionSource<OAuthCallbackResult?> result, CancellationToken stop)
    {
        using (client)
        {
            try
            {
                var stream = client.GetStream();
                using var reader = new StreamReader(stream, Encoding.ASCII, leaveOpen: true);
                var requestLine = await reader.ReadLineAsync(stop) ?? "";

                // Başlıkları sonuna kadar oku: okunmamış veriyle kapanan soket sıfırlanır ve tarayıcı sayfayı göstermeyebilir.
                while (!string.IsNullOrEmpty(await reader.ReadLineAsync(stop)))
                {
                }

                var parsed = OAuthCallback.Parse(requestLine);
                if (parsed is null)
                {
                    await RespondAsync(stream, "404 Not Found", "", stop);
                    return;
                }

                var message = parsed.Error is null
                    ? "CamAda Spotify'a bağlandı. Bu sekmeyi kapatabilirsin."
                    : $"Bağlanılamadı: {WebUtility.HtmlEncode(parsed.Error)}";
                await RespondAsync(stream, "200 OK",
                    "<!doctype html><html lang=\"tr\"><meta charset=\"utf-8\"><title>CamAda</title>" +
                    "<body style=\"font-family:sans-serif;background:#14182A;color:#F4F6FB;display:grid;place-items:center;height:90vh\">" +
                    $"<p style=\"font-size:20px\">{message}</p></body></html>", stop);
                result.TrySetResult(parsed);
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // boş bırakılan ya da yarıda kesilen bağlantı
            }
        }
    }

    private static async Task RespondAsync(Stream stream, string status, string html, CancellationToken cancellation)
    {
        var body = Encoding.UTF8.GetBytes(html);
        var header = Encoding.ASCII.GetBytes(
            $"HTTP/1.1 {status}\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: {body.Length}\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, cancellation);
        await stream.WriteAsync(body, cancellation);
        await stream.FlushAsync(cancellation);
    }
}
