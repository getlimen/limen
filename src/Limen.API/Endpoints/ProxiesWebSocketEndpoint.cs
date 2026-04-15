using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Limen.Application.Common.Interfaces;
using Limen.Application.Services;
using Limen.Contracts.ProxyMessages;
using Limen.Infrastructure.Agents;
using Microsoft.EntityFrameworkCore;

namespace Limen.API.Endpoints;

public static class ProxiesWebSocketEndpoint
{
    public static IEndpointRouteBuilder MapProxiesWebSocket(this IEndpointRouteBuilder app)
    {
        app.Map("/api/proxies/ws", HandleAsync);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext ctx,
        IServiceScopeFactory scopeFactory,
        IProxyConnectionRegistry registry,
        ProxyConfigPusher pusher,
        ILogger<AgentWebSocketChannel> logger,
        CancellationToken abort)
    {
        if (!ctx.WebSockets.IsWebSocketRequest)
        {
            return Results.BadRequest("Expected WebSocket");
        }

        using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
        var channel = new AgentWebSocketChannel(ws);

        var buf = new byte[16 * 1024];
        var first = await ws.ReceiveAsync(buf, abort);
        if (first.MessageType != WebSocketMessageType.Text)
        {
            await ws.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "expected text", CancellationToken.None);
            return Results.Empty;
        }

        using var firstDoc = JsonDocument.Parse(Encoding.UTF8.GetString(buf, 0, first.Count));
        var type = firstDoc.RootElement.GetProperty("Type").GetString();
        if (type != ProxyMessageTypes.ProxyAuth)
        {
            await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "expected proxyAuth", CancellationToken.None);
            return Results.Empty;
        }

        var authPayload = firstDoc.RootElement.GetProperty("Payload").Deserialize<ProxyAuth>();
        if (authPayload is null || !Guid.TryParse(authPayload.ProxyNodeId, out var proxyNodeId))
        {
            await ws.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "bad proxyAuth payload", CancellationToken.None);
            return Results.Empty;
        }

        // Authenticate using a short-lived scope
        await using (var authScope = scopeFactory.CreateAsyncScope())
        {
            var db = authScope.ServiceProvider.GetRequiredService<IAppDbContext>();

            var node = await db.Nodes.FindAsync(new object[] { proxyNodeId }, abort);
            if (node is null || !node.Roles.Any(r => string.Equals(r, "proxy", StringComparison.OrdinalIgnoreCase)))
            {
                await channel.SendJsonAsync(ProxyMessageTypes.ProxyAuthResponse, new ProxyAuthResponse(false, "node is not a proxy"), CancellationToken.None);
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "not a proxy node", CancellationToken.None);
                return Results.Empty;
            }

            var agent = await db.Agents.FirstOrDefaultAsync(a => a.NodeId == proxyNodeId, abort);
            if (agent is null)
            {
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "no agent for node", CancellationToken.None);
                return Results.Empty;
            }

            var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(authPayload.Secret));
            if (!agent.SecretHash.SequenceEqual(secretHash))
            {
                await channel.SendJsonAsync(ProxyMessageTypes.ProxyAuthResponse, new ProxyAuthResponse(false, "bad secret"), CancellationToken.None);
                await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "auth failed", CancellationToken.None);
                return Results.Empty;
            }
        }

        await channel.SendJsonAsync(ProxyMessageTypes.ProxyAuthResponse, new ProxyAuthResponse(true, null), abort);
        await registry.RegisterAsync(proxyNodeId, channel);

        try
        {
            await pusher.PushFullAsync(proxyNodeId, abort);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "initial route set push failed for proxy {ProxyNodeId}", proxyNodeId);
        }

        try
        {
            while (ws.State == WebSocketState.Open && !abort.IsCancellationRequested)
            {
                var r = await ws.ReceiveAsync(buf, abort);
                if (r.MessageType == WebSocketMessageType.Close)
                {
                    break;
                }
                // v1: no inbound messages from proxy are acted on. Future: ack / cert-renewal events.
            }
        }
        catch (Exception ex) { logger.LogWarning(ex, "proxy ws loop exited"); }
        finally { registry.Unregister(proxyNodeId); }

        return Results.Empty;
    }
}
