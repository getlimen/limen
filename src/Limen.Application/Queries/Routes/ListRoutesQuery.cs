using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Routes;

public sealed record RouteDto(
    Guid Id,
    Guid ServiceId,
    Guid ProxyNodeId,
    string Hostname,
    bool TlsEnabled,
    string AuthPolicy);

public sealed record ListRoutesQuery() : IQuery<IReadOnlyList<RouteDto>>;

internal sealed class ListRoutesQueryHandler : IQueryHandler<ListRoutesQuery, IReadOnlyList<RouteDto>>
{
    private readonly IAppDbContext _db;
    public ListRoutesQueryHandler(IAppDbContext db) { _db = db; }

    public async ValueTask<IReadOnlyList<RouteDto>> Handle(ListRoutesQuery q, CancellationToken ct)
        => await _db.PublicRoutes
            .Select(r => new RouteDto(r.Id, r.ServiceId, r.ProxyNodeId, r.Hostname, r.TlsEnabled, r.AuthPolicy))
            .ToListAsync(ct);
}
