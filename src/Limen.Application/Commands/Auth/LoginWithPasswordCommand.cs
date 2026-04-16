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
    // A real Argon2id hash of an empty string; used only for timing equalization when no policy exists.
    // The result is always discarded — this is never a valid credential.
    private const string DummyHash = "$argon2id$v=19$m=65536,t=3,p=1$YWJjZGVmZ2hpams=$dummyhashdummyhashdummyhashdumx=";

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
            _ = _hasher.Verify(cmd.Password, DummyHash); // timing equalizer; result ignored
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
