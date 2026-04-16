using Limen.Application.Common.Interfaces;
using Limen.Domain.Auth;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Commands.Auth;

public sealed record SetResourceAuthPolicyCommand(
    Guid RouteId,
    string Mode,
    string? Password,
    string CookieScope,
    string[]? AllowedEmails) : ICommand<Unit>;

internal sealed class SetResourceAuthPolicyCommandHandler : ICommandHandler<SetResourceAuthPolicyCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IPasswordHasher _hasher;

    public SetResourceAuthPolicyCommandHandler(IAppDbContext db, IClock clock, IPasswordHasher hasher)
    {
        _db = db;
        _clock = clock;
        _hasher = hasher;
    }

    public async ValueTask<Unit> Handle(SetResourceAuthPolicyCommand cmd, CancellationToken ct)
    {
        var now = _clock.UtcNow;

        var policy = await _db.ResourceAuthPolicies
            .FirstOrDefaultAsync(p => p.RouteId == cmd.RouteId, ct);

        if (policy is null)
        {
            policy = new ResourceAuthPolicy
            {
                Id = Guid.NewGuid(),
                RouteId = cmd.RouteId,
                Mode = cmd.Mode,
                CookieScope = cmd.CookieScope,
                CreatedAt = now,
                UpdatedAt = now,
            };
            _db.ResourceAuthPolicies.Add(policy);
        }
        else
        {
            policy.Mode = cmd.Mode;
            policy.CookieScope = cmd.CookieScope;
            policy.UpdatedAt = now;
        }

        if (cmd.Password is not null && cmd.Mode == "password")
        {
            policy.PasswordHash = _hasher.Hash(cmd.Password);
        }

        if (cmd.Mode == "allowlist")
        {
            var existing = await _db.AllowlistedEmails
                .Where(e => e.RouteId == cmd.RouteId)
                .ToListAsync(ct);
            foreach (var e in existing)
            {
                _db.AllowlistedEmails.Remove(e);
            }
            foreach (var email in cmd.AllowedEmails ?? Array.Empty<string>())
            {
                _db.AllowlistedEmails.Add(new AllowlistedEmail
                {
                    Id = Guid.NewGuid(),
                    RouteId = cmd.RouteId,
                    Email = email.ToLowerInvariant(),
                    AddedAt = now,
                });
            }
        }

        var route = await _db.PublicRoutes.FindAsync([cmd.RouteId], ct);
        if (route is not null)
        {
            route.AuthPolicy = cmd.Mode;
        }

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
