using Limen.Application.Common.Interfaces;
using Limen.Domain.Deployments;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Deployments;

public sealed record DeploymentDto(
    Guid Id,
    Guid ServiceId,
    Guid TargetNodeId,
    string ImageDigest,
    string ImageTag,
    string Status,
    string CurrentStage,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? EndedAt);

public sealed record ListDeploymentsQuery(Guid? ServiceId) : IQuery<IReadOnlyList<DeploymentDto>>;

internal sealed class ListDeploymentsQueryHandler : IQueryHandler<ListDeploymentsQuery, IReadOnlyList<DeploymentDto>>
{
    private readonly IAppDbContext _db;

    public ListDeploymentsQueryHandler(IAppDbContext db) { _db = db; }

    public async ValueTask<IReadOnlyList<DeploymentDto>> Handle(ListDeploymentsQuery q, CancellationToken ct)
    {
        var query = _db.Deployments.AsQueryable();

        if (q.ServiceId.HasValue)
        {
            query = query.Where(d => d.ServiceId == q.ServiceId.Value);
        }

        return await query
            .OrderByDescending(d => d.QueuedAt)
            .Select(d => new DeploymentDto(
                d.Id,
                d.ServiceId,
                d.TargetNodeId,
                d.ImageDigest,
                d.ImageTag,
                d.Status.ToString(),
                d.CurrentStage,
                d.QueuedAt,
                d.StartedAt,
                d.EndedAt))
            .ToListAsync(ct);
    }
}
