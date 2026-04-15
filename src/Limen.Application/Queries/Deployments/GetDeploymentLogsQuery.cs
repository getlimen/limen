using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Deployments;

public sealed record GetDeploymentLogsQuery(Guid DeploymentId) : IQuery<string?>;

internal sealed class GetDeploymentLogsQueryHandler : IQueryHandler<GetDeploymentLogsQuery, string?>
{
    private readonly IAppDbContext _db;

    public GetDeploymentLogsQueryHandler(IAppDbContext db) { _db = db; }

    public async ValueTask<string?> Handle(GetDeploymentLogsQuery q, CancellationToken ct)
        => await _db.Deployments
            .Where(d => d.Id == q.DeploymentId)
            .Select(d => d.Logs)
            .FirstOrDefaultAsync(ct);
}
