using Limen.Application.Common.Interfaces;
using Limen.Domain.Deployments;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Limen.Application.Commands.Deployments;

public sealed record CreateDeploymentCommand(
    Guid ServiceId,
    string ImageDigest,
    string ImageTag,
    Guid? PreviousDeploymentId = null) : ICommand<Guid>;

internal sealed class CreateDeploymentCommandHandler : ICommandHandler<CreateDeploymentCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IDeploymentDispatcher _dispatcher;
    private readonly ILogger<CreateDeploymentCommandHandler> _log;

    public CreateDeploymentCommandHandler(
        IAppDbContext db,
        IClock clock,
        IDeploymentDispatcher dispatcher,
        ILogger<CreateDeploymentCommandHandler> log)
    {
        _db = db;
        _clock = clock;
        _dispatcher = dispatcher;
        _log = log;
    }

    public async ValueTask<Guid> Handle(CreateDeploymentCommand cmd, CancellationToken ct)
    {
        var service = await _db.Services.FindAsync(new object[] { cmd.ServiceId }, ct)
            ?? throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");

        var existing = await _db.Deployments
            .Where(d => d.ServiceId == cmd.ServiceId
                && d.ImageDigest == cmd.ImageDigest
                && (d.Status == DeploymentStatus.Queued || d.Status == DeploymentStatus.InProgress))
            .Select(d => d.Id)
            .FirstOrDefaultAsync(ct);

        if (existing != Guid.Empty)
        {
            return existing;
        }

        var deployment = new Deployment
        {
            Id = Guid.NewGuid(),
            ServiceId = cmd.ServiceId,
            TargetNodeId = service.TargetNodeId,
            ImageDigest = cmd.ImageDigest,
            ImageTag = cmd.ImageTag,
            Status = DeploymentStatus.Queued,
            CurrentStage = string.Empty,
            QueuedAt = _clock.UtcNow,
            Logs = string.Empty,
            PreviousDeploymentId = cmd.PreviousDeploymentId,
        };

        _db.Deployments.Add(deployment);
        await _db.SaveChangesAsync(ct);

        try
        {
            await _dispatcher.DispatchAsync(deployment.Id, ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Dispatch failed for deployment {DeploymentId}; it remains Queued", deployment.Id);
        }

        return deployment.Id;
    }
}
