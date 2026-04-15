using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Limen.Application.Commands.Nodes;
using Limen.Application.Common.Interfaces;
using Limen.Contracts.AgentMessages;
using Limen.Domain.Nodes;
using Limen.Infrastructure.Agents;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.API.Endpoints;

public static class AgentsWebSocketEndpoint
{
    public static IEndpointRouteBuilder MapAgentsWebSocket(this IEndpointRouteBuilder app)
    {
        app.Map("/api/agents/ws", HandleAsync);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpContext ctx,
        IMediator mediator,
        IAppDbContext db,
        IAgentConnectionRegistry registry,
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
        Guid agentId;

        try
        {
            if (type == AgentMessageTypes.Enroll)
            {
                var payload = firstDoc.RootElement.GetProperty("Payload").Deserialize<EnrollRequest>()!;
                var result = await mediator.Send(new EnrollAgentCommand(
                    payload.ProvisioningKey, payload.Hostname, payload.Roles, payload.Platform, payload.AgentVersion),
                    abort);
                agentId = result.AgentId;
                await channel.SendJsonAsync(
                    AgentMessageTypes.EnrollResponse,
                    new EnrollResponse(result.AgentId, result.Secret, string.Empty),
                    abort);
            }
            else if (type == AgentMessageTypes.Heartbeat)
            {
                var authHeader = ctx.Request.Headers.Authorization.ToString();
                if (!authHeader.StartsWith("Bearer ", StringComparison.Ordinal))
                {
                    await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "missing credentials", CancellationToken.None);
                    return Results.Empty;
                }
                var parts = authHeader[7..].Split(':');
                if (parts.Length != 2 || !Guid.TryParse(parts[0], out agentId))
                {
                    await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "bad credentials", CancellationToken.None);
                    return Results.Empty;
                }
                var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(parts[1]));
                var agent = await db.Agents.FindAsync(new object[] { agentId }, abort);
                if (agent is null || !agent.SecretHash.SequenceEqual(secretHash))
                {
                    await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "auth failed", CancellationToken.None);
                    return Results.Empty;
                }
            }
            else
            {
                await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "unexpected first frame", CancellationToken.None);
                return Results.Empty;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Agent WS handshake failed");
            if (ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.InternalServerError, "handshake error", CancellationToken.None);
            }
            return Results.Empty;
        }

        await registry.RegisterAsync(agentId, channel);

        var nodeForAgent = await db.Agents
            .Where(a => a.Id == agentId)
            .Select(a => a.NodeId)
            .FirstOrDefaultAsync(abort);
        var node = nodeForAgent == Guid.Empty ? null : await db.Nodes.FindAsync(new object[] { nodeForAgent }, abort);
        if (node is not null)
        {
            node.Status = NodeStatus.Active;
            node.LastSeenAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(abort);
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
                if (r.MessageType != WebSocketMessageType.Text)
                {
                    continue;
                }

                using var msg = JsonDocument.Parse(Encoding.UTF8.GetString(buf, 0, r.Count));
                var msgType = msg.RootElement.GetProperty("Type").GetString();
                if (msgType == AgentMessageTypes.Heartbeat && node is not null)
                {
                    node.LastSeenAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(abort);
                    await channel.SendJsonAsync(AgentMessageTypes.HeartbeatAck, new HeartbeatAck(0), abort);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Agent WS loop exited");
        }
        finally
        {
            registry.Unregister(agentId);
            if (node is not null)
            {
                node.Status = NodeStatus.Disconnected;
                try
                {
                    await db.SaveChangesAsync(CancellationToken.None);
                }
                catch (Exception e)
                {
                    logger.LogWarning(e, "Failed to persist disconnected status for node {NodeId}", node.Id);
                }
            }
        }

        return Results.Empty;
    }
}
