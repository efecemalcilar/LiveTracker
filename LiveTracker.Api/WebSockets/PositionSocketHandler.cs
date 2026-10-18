using System.Net.WebSockets;
using System.Text.Json;
using LiveTracker.Application.Streaming;

namespace LiveTracker.Api.WebSockets;

public sealed class PositionSocketHandler
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private readonly PositionHub _hub;
    private readonly ILogger<PositionSocketHandler> _logger;

    public PositionSocketHandler(PositionHub hub, ILogger<PositionSocketHandler> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    // /ws/positions?lastSeq=123  -> lastSeq verilirse kaçırılanlar tekrar gönderilir
    public async Task HandleAsync(HttpContext ctx)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            ctx.Response.StatusCode = StatusCodes.Status400BadRequest;
            return;
        }

        long? lastSeq = long.TryParse(ctx.Request.Query["lastSeq"], out var s) ? s : null;

        using var socket = await ctx.WebSockets.AcceptWebSocketAsync();
        using var subscription = _hub.Subscribe(lastSeq);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.RequestAborted);

        // İstemci kapatırsa haberdar olmak için sürekli okuyan bir görev
        var receiveTask = WatchForCloseAsync(socket, cts);

        try
        {
            foreach (var message in subscription.Initial)
                await SendAsync(socket, message, cts.Token);

            await foreach (var message in subscription.Reader.ReadAllAsync(cts.Token))
                await SendAsync(socket, message, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // istemci ayrıldı veya uygulama kapanıyor
        }
        catch (Exception ex) when (ex is SlowConsumerException || ex.InnerException is SlowConsumerException)
        {
            _logger.LogWarning("Yavas istemci kapatildi");
            await TryCloseAsync(socket, WebSocketCloseStatus.PolicyViolation,
                "Yavas istemci. lastSeq ile tekrar baglanin.");
        }
        catch (WebSocketException)
        {
            // bağlantı koptu
        }
        finally
        {
            cts.Cancel();
            await receiveTask;
        }
    }

    private static Task SendAsync(WebSocket socket, StreamMessage message, CancellationToken ct)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(message, Json);
        return socket.SendAsync(bytes, WebSocketMessageType.Text, true, ct);
    }

    private static async Task WatchForCloseAsync(WebSocket socket, CancellationTokenSource cts)
    {
        var buffer = new byte[256];
        try
        {
            while (socket.State == WebSocketState.Open)
            {
                var result = await socket.ReceiveAsync(buffer, cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;
            }
        }
        catch { /* iptal veya kopma: sorun değil */ }
        finally { cts.Cancel(); }
    }

    private static async Task TryCloseAsync(WebSocket socket, WebSocketCloseStatus status, string reason)
    {
        try
        {
            if (socket.State == WebSocketState.Open)
                await socket.CloseAsync(status, reason, CancellationToken.None);
        }
        catch { }
    }
}