using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Auth;

public sealed record ResourceAuthPolicyDto(
    Guid RouteId,
    string Mode,
    string CookieScope,
    string[] AllowedEmails);

public sealed record GetResourceAuthPolicyQuery(Guid RouteId) : IQuery<ResourceAuthPolicyDto?>;

internal sealed class GetResourceAuthPolicyQueryHandler
    : IQueryHandler<GetResourceAuthPolicyQuery, ResourceAuthPolicyDto?>
{
    private readonly IAppDbContext _db;

    public GetResourceAuthPolicyQueryHandler(IAppDbContext db)
    {
        _db = db;
    }

    public async ValueTask<ResourceAuthPolicyDto?> Handle(GetResourceAuthPolicyQuery query, CancellationToken ct)
    {
        var policy = await _db.ResourceAuthPolicies
            .FirstOrDefaultAsync(p => p.RouteId == query.RouteId, ct);

        if (policy is null)
        {
            return null;
        }

        var emails = await _db.AllowlistedEmails
            .Where(e => e.RouteId == query.RouteId)
            .Select(e => e.Email)
            .ToArrayAsync(ct);

        // Never include password hash in DTO
        return new ResourceAuthPolicyDto(policy.RouteId, policy.Mode, policy.CookieScope, emails);
    }
}
