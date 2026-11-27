using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using CamAda.Core.Spotify;
using CamAda.Core.Text;

namespace CamAda.Spotify;

/// <summary>
/// Spotify girişinden sonra tarayıcının yönlendirdiği http://127.0.0.1:43821/callback isteğini yakalar. HttpListener yerine
/// TcpListener: 127.0.0.1 için yönetici izni (URL ACL) gerekmez. Bağlantılar ayrı ayrı okunur; tarayıcıların önceden açıp
/// boş bıraktığı bağlantılar asıl isteği bekletmez. Durumu (state) bu girişinkiyle eşleşmeyen geri dönüş (ör. önceki
/// denemenin sekmesi) yok sayılır ve bekleme sürer; eşleşen kod takas edildikten sonra sayfa sonucu söyler. Başka yollar
/// 404 alır.
/// </summary>
public static class LoopbackCallback
{
    /// <summary>
    /// Geri dönüşü bekler ve kodu <paramref name="exchange"/> ile takas eder; takasın sonucunu döner. Kullanıcı reddederse,
    /// takas başarısızsa, zaman aşımında, iptalde ya da port meşgulse null.
    /// </summary>
    public static async Task<T?> WaitAsync<T>(string expectedState, Func<string, Task<T?>> exchange, TimeSpan timeout,
        CancellationToken cancellation) where T : class
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
        var session = new Session<T>(expectedState, exchange);
        using var registration = stop.Token.Register(() => session.Result.TrySetResult(null));
        var accepting = AcceptAsync(listener, session, stop.Token);
        try
        {
            return await session.Result.Task;
        }
        finally
        {
            stop.Cancel();
            listener.Stop();
            await accepting;
        }
    }

    /// <summary>Bir girişin bekleme durumu: beklenen state, takas işlevi, tek seferlik sahiplenme ve sonuç.</summary>
    private sealed class Session<T>(string expectedState, Func<string, Task<T?>> exchange) where T : class
    {
        private int _claimed;

        public string ExpectedState { get; } = expectedState;

        public Func<string, Task<T?>> Exchange { get; } = exchange;

        public TaskCompletionSource<T?> Result { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Kodu yalnız ilk geçerli geri dönüş takas eder (tarayıcı isteği yinelerse ikinci takas olmaz).</summary>
        public bool TryClaim() => Interlocked.Exchange(ref _claimed, 1) == 0;
    }

    private static async Task AcceptAsync<T>(TcpListener listener, Session<T> session, CancellationToken stop) where T : class
    {
        try
        {
            while (!stop.IsCancellationRequested)
            {
                var client = await listener.AcceptTcpClientAsync(stop);
                _ = HandleAsync(client, session, stop);
            }
        }
        catch (Exception ex) when (ex is OperationCanceledException or SocketException or ObjectDisposedException)
        {
            // dinleyici kapandı
        }
    }

    private static async Task HandleAsync<T>(TcpClient client, Session<T> session, CancellationToken stop) where T : class
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

                switch (OAuthCallback.Judge(parsed, session.ExpectedState))
                {
                    case CallbackVerdict.Stale:
                        await RespondAsync(stream, "400 Bad Request",
                            Page(UiStrings.Callback_StaleLink), stop);
                        return;

                    case CallbackVerdict.Denied:
                        await RespondAsync(stream, "200 OK",
                            Page(string.Format(UiStrings.Callback_Failed, WebUtility.HtmlEncode(parsed.Error ?? UiStrings.Callback_MissingResponse))), stop);
                        session.Result.TrySetResult(null);
                        return;
                }

                if (!session.TryClaim())
                {
                    await RespondAsync(stream, "200 OK", Page(UiStrings.Callback_InProgress), stop);
                    return;
                }

                T? exchanged = null;
                try
                {
                    exchanged = await session.Exchange(parsed.Code!);
                    await RespondAsync(stream, "200 OK", Page(exchanged is not null
                        ? UiStrings.Callback_Connected
                        : UiStrings.Callback_NotCompleted), stop);
                }
                finally
                {
                    // Sayfa yazıldıktan sonra (yazılamasa ya da takas hata atsa da) bekleme biter; önce bitseydi dinleyici
                    // kapanır ve sayfa yarıda kalırdı.
                    session.Result.TrySetResult(exchanged);
                }
            }
            catch (Exception ex) when (ex is IOException or SocketException or OperationCanceledException or ObjectDisposedException)
            {
                // boş bırakılan ya da yarıda kesilen bağlantı
            }
        }
    }

    private static string Page(string message) =>
        "<!doctype html><html lang=\"" + System.Globalization.CultureInfo.CurrentUICulture.TwoLetterISOLanguageName + "\"><meta charset=\"utf-8\"><title>CamAda</title>" +
        "<body style=\"font-family:sans-serif;background:#14182A;color:#F4F6FB;display:grid;place-items:center;height:90vh\">" +
        $"<p style=\"font-size:20px\">{message}</p></body></html>";

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
