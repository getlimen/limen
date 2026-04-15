using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Auth;

public sealed record AdminInfoDto(string Subject, string Email, DateTimeOffset ExpiresAt);

public sealed record GetCurrentAdminQuery(Guid SessionId) : IQuery<AdminInfoDto?>;

internal sealed class GetCurrentAdminQueryHandler : IQueryHandler<GetCurrentAdminQuery, AdminInfoDto?>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public GetCurrentAdminQueryHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async ValueTask<AdminInfoDto?> Handle(GetCurrentAdminQuery q, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var session = await _db.AdminSessions
            .Where(s => s.Id == q.SessionId && s.RevokedAt == null && s.ExpiresAt > now)
            .FirstOrDefaultAsync(ct);
        return session is null ? null : new AdminInfoDto(session.Subject, session.Email, session.ExpiresAt);
    }
}
