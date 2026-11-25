using System.IO;
using System.Net.Http;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using CamAda.Core.Assistant;
using CamAda.Core.Audio;

namespace CamAda.Assistant;

/// <summary>
/// Gemini Live istemcisi (ham WebSocket; denemede doğrulanan protokol): kurulum iletisini gönderir, kurulum tamamlanana
/// kadar bekler, sesi ve işlev sonuçlarını gönderir, sunucu iletilerini <see cref="LiveEvents"/> ile olaylara çevirir.
/// Anahtar hiçbir hata metnine ya da günlüğe girmez.
/// </summary>
internal sealed class GeminiLiveClient : IAsyncDisposable
{
    private const string Endpoint =
        "wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent";

    private static readonly TimeSpan SetupTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan ConnectTimeout = TimeSpan.FromSeconds(10);

    private readonly ClientWebSocket _socket;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly CancellationTokenSource _stop = new();
    private readonly TaskCompletionSource<AssistantError> _setup = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private Task? _receiving;
    private bool _closing;
    private int _closedRaised;

    private GeminiLiveClient(ClientWebSocket socket)
    {
        _socket = socket;
    }

    /// <summary>Sunucu iletisinden çıkan olay (alma iş parçacığında).</summary>
    public event Action<LiveEvent>? Received;

    /// <summary>Bağlantı kapandı; bizim kapatmamızda <see cref="AssistantError.None"/> (alma iş parçacığında).</summary>
    public event Action<AssistantError>? Closed;

    /// <summary>
    /// Bağlanır, kurulumu gönderir ve <c>setupComplete</c> bekler; başaramazsa <see cref="AssistantException"/>. Anahtar
    /// <c>x-goog-api-key</c> başlığıyla gider, URL'ye hiç girmez (Plan 5 Görev 5'te doğrulandı).
    /// </summary>
    public static async Task<GeminiLiveClient> ConnectAsync(string apiKey, JsonObject setup, CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        socket.Options.CollectHttpResponseDetails = true;
        socket.Options.SetRequestHeader("x-goog-api-key", apiKey);
        // Oturumun iptali bağlanma adımına geçirilmez: TCP kurulurken iptal edilen ClientWebSocket atılsa da .NET'in iç
        // bağlantısı açık kalıyordu (deneme: kapanıştan 13 sn sonra Google'a açık bağlantı). Bağlanma kendi zaman aşımıyla
        // biter, iptal hemen ardından fark edilip bağlantı düzgünce kapatılır.
        using var connectTimeout = new CancellationTokenSource(ConnectTimeout);
        try
        {
            await socket.ConnectAsync(new Uri(Endpoint), connectTimeout.Token);
        }
        catch (Exception ex) when (ex is WebSocketException or HttpRequestException or IOException or OperationCanceledException)
        {
            var status = (int)socket.HttpStatusCode;
            socket.Dispose();
            throw new AssistantException(status == 0 ? AssistantError.Network : LiveErrors.FromHttp(status, null), "Gemini'ye bağlanılamadı.");
        }

        var client = new GeminiLiveClient(socket);
        client._receiving = Task.Run(client.ReceiveLoopAsync);
        try
        {
            ct.ThrowIfCancellationRequested();
            await client.SendAsync(setup);
            var done = await Task.WhenAny(client._setup.Task, Task.Delay(SetupTimeout, ct));
            var error = done == client._setup.Task ? client._setup.Task.Result : AssistantError.Network;
            if (error != AssistantError.None)
            {
                await client.DisposeAsync();
                throw new AssistantException(error, "Gemini oturumu kurulamadı.");
            }
        }
        catch (OperationCanceledException)
        {
            await client.DisposeAsync();
            throw;
        }

#if DEBUG
        if (int.TryParse(Environment.GetEnvironmentVariable("CAMADA_ADA_FAIL_AFTER"), out var failAfter))
        {
            _ = Task.Delay(TimeSpan.FromSeconds(failAfter)).ContinueWith(_ => socket.Abort(), TaskScheduler.Default);
        }
#endif
        return client;
    }

    public Task SendAudioAsync(short[] pcm16k) => SendAsync(LiveMessages.Audio(Pcm.ToBytes(pcm16k)));

    public Task SendToolResultsAsync(IReadOnlyList<ToolResult> results) => SendAsync(LiveMessages.ToolResponse(results));

    /// <summary>Bağlantıyı düzgünce kapatır (en çok 2 sn bekler).</summary>
    public async Task CloseAsync()
    {
        if (_closing)
        {
            return;
        }

        _closing = true;
        try
        {
            if (_socket.State == WebSocketState.Open)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await _socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "bitti", timeout.Token);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
        }

        _stop.Cancel();
        if (_receiving is not null)
        {
            await Task.WhenAny(_receiving, Task.Delay(2000));
        }

        RaiseClosed(AssistantError.None);
    }

    public async ValueTask DisposeAsync()
    {
        await CloseAsync();
        _socket.Dispose();
        _stop.Dispose();
    }

    private async Task SendAsync(JsonObject message)
    {
        var bytes = Encoding.UTF8.GetBytes(message.ToJsonString());
        await _sendLock.WaitAsync();
        try
        {
            if (_socket.State == WebSocketState.Open && !_closing)
            {
                await _socket.SendAsync(bytes, WebSocketMessageType.Text, true, _stop.Token);
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            // kapanış alma döngüsünde bildirilir
        }
        finally
        {
            _sendLock.Release();
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = new byte[256 * 1024];
        using var message = new MemoryStream();
        var error = AssistantError.Network;
        try
        {
            while (_socket.State == WebSocketState.Open)
            {
                message.SetLength(0);
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, _stop.Token);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        error = _closing ? AssistantError.None : LiveErrors.FromClose(_socket.CloseStatusDescription);
                        return;
                    }

                    message.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                foreach (var e in LiveEvents.Parse(message.ToArray()))
                {
                    if (e is LiveEvent.SetupComplete)
                    {
                        _setup.TrySetResult(AssistantError.None);
                    }

                    Received?.Invoke(e);
                }
            }
        }
        catch (Exception ex) when (ex is WebSocketException or OperationCanceledException or ObjectDisposedException)
        {
            error = _closing ? AssistantError.None : AssistantError.Network;
        }
        finally
        {
            _setup.TrySetResult(error == AssistantError.None ? AssistantError.Network : error);
            RaiseClosed(error);
        }
    }

    private void RaiseClosed(AssistantError error)
    {
        if (Interlocked.Exchange(ref _closedRaised, 1) == 0)
        {
            Closed?.Invoke(error);
        }
    }
}
