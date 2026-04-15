using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Limen.Application.Commands.Deployments;
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
        IServiceScopeFactory scopeFactory,
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
                await using var scope = scopeFactory.CreateAsyncScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                var result = await mediator.Send(new EnrollAgentCommand(
                    payload.ProvisioningKey, payload.Hostname, payload.Roles, payload.Platform, payload.AgentVersion),
                    abort);
                agentId = result.AgentId;
                await channel.SendJsonAsync(
                    AgentMessageTypes.EnrollResponse,
                    new EnrollResponse(result.AgentId, result.Secret, result.Wireguard),
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
                await using var scope = scopeFactory.CreateAsyncScope();
                var db = scope.ServiceProvider.GetRequiredService<IAppDbContext>();
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

        // Update node status to Active using a short-lived scope
        await using (var handshakeScope = scopeFactory.CreateAsyncScope())
        {
            var db = handshakeScope.ServiceProvider.GetRequiredService<IAppDbContext>();
            var nodeId = await db.Agents
                .Where(a => a.Id == agentId)
                .Select(a => a.NodeId)
                .FirstOrDefaultAsync(abort);
            if (nodeId != Guid.Empty)
            {
                var node = await db.Nodes.FindAsync(new object[] { nodeId }, abort);
                if (node is not null)
                {
                    node.Status = NodeStatus.Active;
                    node.LastSeenAt = DateTimeOffset.UtcNow;
                    await db.SaveChangesAsync(abort);
                }
            }
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

                await using var frameScope = scopeFactory.CreateAsyncScope();
                var frameDb = frameScope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var frameMediatorSvc = frameScope.ServiceProvider;

                if (msgType == AgentMessageTypes.Heartbeat)
                {
                    var nodeId = await frameDb.Agents
                        .Where(a => a.Id == agentId)
                        .Select(a => a.NodeId)
                        .FirstOrDefaultAsync(abort);
                    if (nodeId != Guid.Empty)
                    {
                        var node = await frameDb.Nodes.FindAsync(new object[] { nodeId }, abort);
                        if (node is not null)
                        {
                            node.LastSeenAt = DateTimeOffset.UtcNow;
                            await frameDb.SaveChangesAsync(abort);
                        }
                    }
                    await channel.SendJsonAsync(AgentMessageTypes.HeartbeatAck, new HeartbeatAck(0), abort);
                }
                else if (msgType == AgentMessageTypes.DeployProgress)
                {
                    var progress = msg.RootElement.GetProperty("Payload").Deserialize<DeployProgress>()!;
                    var mediator = frameMediatorSvc.GetRequiredService<IMediator>();
                    await mediator.Send(new ReportDeploymentProgressCommand(
                        progress.DeploymentId, progress.Stage, progress.Message, progress.PercentComplete), abort);
                }
                else if (msgType == AgentMessageTypes.DeployResult)
                {
                    var result = msg.RootElement.GetProperty("Payload").Deserialize<DeployResult>()!;
                    var mediator = frameMediatorSvc.GetRequiredService<IMediator>();
                    await mediator.Send(new ReportDeploymentResultCommand(
                        result.DeploymentId, result.Success, result.RolledBackReason), abort);
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
            // Update node status to Disconnected using a fresh scope
            await using var disconnectScope = scopeFactory.CreateAsyncScope();
            try
            {
                var db = disconnectScope.ServiceProvider.GetRequiredService<IAppDbContext>();
                var nodeId = await db.Agents
                    .Where(a => a.Id == agentId)
                    .Select(a => a.NodeId)
                    .FirstOrDefaultAsync(CancellationToken.None);
                if (nodeId != Guid.Empty)
                {
                    var node = await db.Nodes.FindAsync(new object[] { nodeId }, CancellationToken.None);
                    if (node is not null)
                    {
                        node.Status = NodeStatus.Disconnected;
                        await db.SaveChangesAsync(CancellationToken.None);
                    }
                }
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Failed to persist disconnected status for agent {AgentId}", agentId);
            }
        }

        return Results.Empty;
    }
}
