using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Auth;

public sealed record PublicRoutePolicyDto(Guid RouteId, string Mode, string Hostname);

public sealed record GetPublicRoutePolicyQuery(Guid RouteId) : IQuery<PublicRoutePolicyDto?>;

internal sealed class GetPublicRoutePolicyQueryHandler
    : IQueryHandler<GetPublicRoutePolicyQuery, PublicRoutePolicyDto?>
{
    private readonly IAppDbContext _db;

    public GetPublicRoutePolicyQueryHandler(IAppDbContext db) { _db = db; }

    public async ValueTask<PublicRoutePolicyDto?> Handle(GetPublicRoutePolicyQuery q, CancellationToken ct)
    {
        var route = await _db.PublicRoutes
            .Where(r => r.Id == q.RouteId)
            .Select(r => new { r.Id, r.Hostname, r.AuthPolicy })
            .FirstOrDefaultAsync(ct);
        if (route is null) { return null; }
        return new PublicRoutePolicyDto(route.Id, route.AuthPolicy, route.Hostname);
    }
}
