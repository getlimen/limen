using Limen.Application.Common.Interfaces;
using Limen.Application.Services;
using Limen.Domain.Auth;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Commands.Auth;

public sealed record LoginWithPasswordResult(string Jwt, DateTimeOffset ExpiresAt);

public sealed record LoginWithPasswordCommand(
    Guid RouteId,
    string Email,
    string Password) : ICommand<LoginWithPasswordResult?>;

internal sealed class LoginWithPasswordCommandHandler
    : ICommandHandler<LoginWithPasswordCommand, LoginWithPasswordResult?>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IPasswordHasher _hasher;
    private readonly JwtBuilder _jwt;

    public LoginWithPasswordCommandHandler(
        IAppDbContext db,
        IClock clock,
        IPasswordHasher hasher,
        JwtBuilder jwt)
    {
        _db = db;
        _clock = clock;
        _hasher = hasher;
        _jwt = jwt;
    }

    public async ValueTask<LoginWithPasswordResult?> Handle(LoginWithPasswordCommand cmd, CancellationToken ct)
    {
        var policy = await _db.ResourceAuthPolicies
            .FirstOrDefaultAsync(p => p.RouteId == cmd.RouteId, ct);

        if (policy is null || policy.Mode != "password" || policy.PasswordHash is null)
        {
            return null;
        }

        if (!_hasher.Verify(cmd.Password, policy.PasswordHash))
        {
            return null;
        }

        var (jwt, jti, exp) = _jwt.Build(cmd.RouteId, cmd.Email, "password", policy.CookieScope);

        _db.IssuedTokens.Add(new IssuedToken
        {
            Id = jti,
            Subject = cmd.Email,
            RouteId = cmd.RouteId,
            IssuedAt = _clock.UtcNow,
            ExpiresAt = exp,
        });

        await _db.SaveChangesAsync(ct);
        return new LoginWithPasswordResult(jwt, exp);
    }
}
