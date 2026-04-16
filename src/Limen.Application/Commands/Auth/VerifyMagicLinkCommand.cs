using System.Security.Cryptography;
using Limen.Application.Common.Interfaces;
using Limen.Application.Services;
using Limen.Domain.Auth;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Commands.Auth;

public sealed record VerifyMagicLinkResult(string Jwt, DateTimeOffset ExpiresAt, Guid RouteId);

public sealed record VerifyMagicLinkCommand(string Token, Guid RouteId) : ICommand<VerifyMagicLinkResult?>;

internal sealed class VerifyMagicLinkCommandHandler
    : ICommandHandler<VerifyMagicLinkCommand, VerifyMagicLinkResult?>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly JwtBuilder _jwt;

    public VerifyMagicLinkCommandHandler(IAppDbContext db, IClock clock, JwtBuilder jwt)
    {
        _db = db;
        _clock = clock;
        _jwt = jwt;
    }

    public async ValueTask<VerifyMagicLinkResult?> Handle(VerifyMagicLinkCommand cmd, CancellationToken ct)
    {
        var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(cmd.Token));
        var tokenHash = Convert.ToBase64String(hashBytes);

        var now = _clock.UtcNow;

        var magicLink = await _db.MagicLinks
            .FirstOrDefaultAsync(m => m.TokenHash == tokenHash, ct);

        if (magicLink is null || magicLink.RouteId != cmd.RouteId)
        {
            return null;
        }

        if (magicLink.UsedAt is not null || magicLink.ExpiresAt < now)
        {
            return null;
        }

        var policy = await _db.ResourceAuthPolicies
            .FirstOrDefaultAsync(p => p.RouteId == cmd.RouteId, ct);

        if (policy is null)
        {
            return null;
        }

        magicLink.UsedAt = now;

        var (jwt, jti, exp) = _jwt.Build(cmd.RouteId, magicLink.Email, "allowlist", policy.CookieScope);

        _db.IssuedTokens.Add(new IssuedToken
        {
            Id = jti,
            Subject = magicLink.Email,
            RouteId = cmd.RouteId,
            IssuedAt = now,
            ExpiresAt = exp,
        });

        await _db.SaveChangesAsync(ct);
        return new VerifyMagicLinkResult(jwt, exp, cmd.RouteId);
    }
}
