using Limen.Application.Common.Interfaces;
using Limen.Contracts.AgentMessages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Limen.Infrastructure.Deployments;

public sealed class DeploymentDispatcher : IDeploymentDispatcher
{
    private readonly IAppDbContext _db;
    private readonly IAgentConnectionRegistry _registry;
    private readonly ILogger<DeploymentDispatcher> _log;

    public DeploymentDispatcher(
        IAppDbContext db,
        IAgentConnectionRegistry registry,
        ILogger<DeploymentDispatcher> log)
    {
        _db = db;
        _registry = registry;
        _log = log;
    }

    public async Task<bool> DispatchAsync(Guid deploymentId, CancellationToken ct)
    {
        var deployment = await _db.Deployments.FindAsync(new object[] { deploymentId }, ct);
        if (deployment is null)
        {
            _log.LogWarning("Deployment {DeploymentId} not found for dispatch", deploymentId);
            return false;
        }

        var service = await _db.Services.FindAsync(new object[] { deployment.ServiceId }, ct);
        if (service is null)
        {
            _log.LogWarning("Service {ServiceId} not found for deployment {DeploymentId}", deployment.ServiceId, deploymentId);
            return false;
        }

        var agent = await _db.Agents
            .Where(a => a.NodeId == deployment.TargetNodeId)
            .FirstOrDefaultAsync(ct);

        if (agent is null)
        {
            _log.LogInformation("No agent for node {NodeId}; deployment {DeploymentId} stays Queued", deployment.TargetNodeId, deploymentId);
            return false;
        }

        var channel = _registry.Get(agent.Id);
        if (channel is null)
        {
            _log.LogInformation("Agent {AgentId} not connected; deployment {DeploymentId} stays Queued", agent.Id, deploymentId);
            return false;
        }

        var deployCommand = new DeployCommand(
            DeploymentId: deployment.Id,
            ServiceId: deployment.ServiceId,
            Image: service.Image,
            ContainerName: service.ContainerName,
            InternalPort: service.InternalPort,
            Env: new Dictionary<string, string>(),
            Volumes: Array.Empty<string>(),
            HealthCheck: new HealthCheckSpec(null, 30, 3, 10),
            NetworkMode: "bridge");

        await channel.SendJsonAsync(AgentMessageTypes.Deploy, deployCommand, ct);

        _log.LogInformation("Dispatched deployment {DeploymentId} to agent {AgentId}", deploymentId, agent.Id);
        return true;
    }
}
