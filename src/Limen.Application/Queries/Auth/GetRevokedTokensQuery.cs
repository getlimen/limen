using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Auth;

public sealed record RevokedTokenDto(Guid Jti, long ExpiresAtUnix);

public sealed record GetRevokedTokensQuery() : IQuery<IReadOnlyList<RevokedTokenDto>>;

internal sealed class GetRevokedTokensQueryHandler
    : IQueryHandler<GetRevokedTokensQuery, IReadOnlyList<RevokedTokenDto>>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public GetRevokedTokensQueryHandler(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async ValueTask<IReadOnlyList<RevokedTokenDto>> Handle(GetRevokedTokensQuery query, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        return await _db.IssuedTokens
            .Where(t => t.RevokedAt != null && t.ExpiresAt > now)
            .Select(t => new RevokedTokenDto(t.Id, t.ExpiresAt.ToUnixTimeSeconds()))
            .ToListAsync(ct);
    }
}
