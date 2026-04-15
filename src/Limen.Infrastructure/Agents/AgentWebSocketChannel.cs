using System.Net.WebSockets;
using System.Text.Json;
using Limen.Application.Common.Interfaces;
using Limen.Contracts.Common;

namespace Limen.Infrastructure.Agents;

public sealed class AgentWebSocketChannel : IAgentChannel
{
    private readonly WebSocket _ws;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public AgentWebSocketChannel(WebSocket ws) => _ws = ws;

    public async Task SendJsonAsync<T>(string type, T payload, CancellationToken ct)
    {
        var env = new Envelope<T>(type, ConfigVersion.Zero, payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(env);
        await _sendLock.WaitAsync(ct);
        try { await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct); }
        finally { _sendLock.Release(); }
    }

    public async Task CloseAsync()
    {
        if (_ws.State == WebSocketState.Open)
        {
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "server-close", CancellationToken.None);
        }
    }
}
