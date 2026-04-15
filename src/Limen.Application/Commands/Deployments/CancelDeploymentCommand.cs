using Limen.Application.Common.Interfaces;
using Limen.Contracts.AgentMessages;
using Limen.Domain.Deployments;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Limen.Application.Commands.Deployments;

public sealed record CancelDeploymentCommand(Guid DeploymentId) : ICommand;

internal sealed class CancelDeploymentCommandHandler : ICommandHandler<CancelDeploymentCommand>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IAgentConnectionRegistry _registry;
    private readonly ILogger<CancelDeploymentCommandHandler> _log;

    public CancelDeploymentCommandHandler(
        IAppDbContext db,
        IClock clock,
        IAgentConnectionRegistry registry,
        ILogger<CancelDeploymentCommandHandler> log)
    {
        _db = db;
        _clock = clock;
        _registry = registry;
        _log = log;
    }

    public async ValueTask<Unit> Handle(CancelDeploymentCommand cmd, CancellationToken ct)
    {
        var deployment = await _db.Deployments.FindAsync(new object[] { cmd.DeploymentId }, ct)
            ?? throw new InvalidOperationException($"Deployment {cmd.DeploymentId} not found.");

        if (deployment.Status != DeploymentStatus.Queued && deployment.Status != DeploymentStatus.InProgress)
        {
            return Unit.Value;
        }

        var wasInProgress = deployment.Status == DeploymentStatus.InProgress;
        var now = _clock.UtcNow;
        deployment.Status = DeploymentStatus.Cancelled;
        deployment.EndedAt = now;

        if (wasInProgress)
        {
            var agent = await _db.Agents
                .Where(a => a.NodeId == deployment.TargetNodeId)
                .FirstOrDefaultAsync(ct);

            if (agent is not null)
            {
                var channel = _registry.Get(agent.Id);
                if (channel is not null)
                {
                    try
                    {
                        var service = await _db.Services.FindAsync(new object[] { deployment.ServiceId }, ct);
                        if (service is not null)
                        {
                            await channel.SendJsonAsync(
                                AgentMessageTypes.StopContainer,
                                new StopContainerCommand(service.ContainerName),
                                ct);
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.LogWarning(ex, "Best-effort StopContainer failed for deployment {DeploymentId}", cmd.DeploymentId);
                    }
                }
            }
        }

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
