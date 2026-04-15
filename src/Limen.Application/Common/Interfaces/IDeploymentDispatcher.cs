namespace Limen.Application.Common.Interfaces;

public interface IDeploymentDispatcher
{
    Task<bool> DispatchAsync(Guid deploymentId, CancellationToken ct);
}
