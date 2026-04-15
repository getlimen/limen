using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Services;

public sealed record ServiceDto(
    Guid Id,
    string Name,
    Guid TargetNodeId,
    string ContainerName,
    int InternalPort,
    string Image,
    bool AutoDeploy);

public sealed record ListServicesQuery() : IQuery<IReadOnlyList<ServiceDto>>;

internal sealed class ListServicesQueryHandler : IQueryHandler<ListServicesQuery, IReadOnlyList<ServiceDto>>
{
    private readonly IAppDbContext _db;
    public ListServicesQueryHandler(IAppDbContext db) { _db = db; }

    public async ValueTask<IReadOnlyList<ServiceDto>> Handle(ListServicesQuery q, CancellationToken ct)
        => await _db.Services
            .Select(s => new ServiceDto(s.Id, s.Name, s.TargetNodeId, s.ContainerName, s.InternalPort, s.Image, s.AutoDeploy))
            .ToListAsync(ct);
}
