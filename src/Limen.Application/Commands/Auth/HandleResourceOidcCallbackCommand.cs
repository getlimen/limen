using Limen.Application.Common.Interfaces;
using Limen.Application.Services;
using Limen.Domain.Auth;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Commands.Auth;

public sealed record HandleResourceOidcCallbackResult(
    string Jwt,
    DateTimeOffset ExpiresAt,
    Guid RouteId,
    string ReturnTo);

public sealed record HandleResourceOidcCallbackCommand(
    string State,
    string Subject,
    string Email) : ICommand<HandleResourceOidcCallbackResult?>;

internal sealed class HandleResourceOidcCallbackCommandHandler
    : ICommandHandler<HandleResourceOidcCallbackCommand, HandleResourceOidcCallbackResult?>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IResourceOidcStateStore _store;
    private readonly JwtBuilder _jwt;

    public HandleResourceOidcCallbackCommandHandler(
        IAppDbContext db,
        IClock clock,
        IResourceOidcStateStore store,
        JwtBuilder jwt)
    {
        _db = db;
        _clock = clock;
        _store = store;
        _jwt = jwt;
    }

    public async ValueTask<HandleResourceOidcCallbackResult?> Handle(
        HandleResourceOidcCallbackCommand cmd, CancellationToken ct)
    {
        var entry = _store.ConsumeState(cmd.State);
        if (entry is null)
        {
            return null;
        }

        var (routeId, returnTo) = entry.Value;

        var policy = await _db.ResourceAuthPolicies
            .FirstOrDefaultAsync(p => p.RouteId == routeId, ct);

        if (policy is null)
        {
            return null;
        }

        var (jwt, jti, exp) = _jwt.Build(routeId, cmd.Email, "sso", policy.CookieScope);

        _db.IssuedTokens.Add(new IssuedToken
        {
            Id = jti,
            Subject = cmd.Email,
            RouteId = routeId,
            IssuedAt = _clock.UtcNow,
            ExpiresAt = exp,
        });

        await _db.SaveChangesAsync(ct);
        return new HandleResourceOidcCallbackResult(jwt, exp, routeId, returnTo);
    }
}
