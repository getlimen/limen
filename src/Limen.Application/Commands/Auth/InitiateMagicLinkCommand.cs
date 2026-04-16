using System.Security.Cryptography;
using Limen.Application.Common.Interfaces;
using Limen.Application.Common.Options;
using Limen.Domain.Auth;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Limen.Application.Commands.Auth;

public sealed record InitiateMagicLinkCommand(Guid RouteId, string Email) : ICommand<Unit>;

internal sealed class InitiateMagicLinkCommandHandler : ICommandHandler<InitiateMagicLinkCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly IMagicLinkSender _sender;
    private readonly IOptions<AuthSettings> _opt;

    public InitiateMagicLinkCommandHandler(
        IAppDbContext db,
        IClock clock,
        IMagicLinkSender sender,
        IOptions<AuthSettings> opt)
    {
        _db = db;
        _clock = clock;
        _sender = sender;
        _opt = opt;
    }

    public async ValueTask<Unit> Handle(InitiateMagicLinkCommand cmd, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(_opt.Value.PublicBaseUrl))
        {
            throw new InvalidOperationException(
                "AuthSettings.PublicBaseUrl must be configured to send magic links.");
        }

        var policy = await _db.ResourceAuthPolicies
            .FirstOrDefaultAsync(p => p.RouteId == cmd.RouteId, ct);

        if (policy is null || policy.Mode != "allowlist")
        {
            return Unit.Value; // anti-enumeration
        }

        var email = cmd.Email.ToLowerInvariant();

        var allowed = await _db.AllowlistedEmails
            .AnyAsync(e => e.RouteId == cmd.RouteId && e.Email == email, ct);

        if (!allowed)
        {
            return Unit.Value; // anti-enumeration
        }

        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        var tokenHash = Convert.ToBase64String(hashBytes);

        var now = _clock.UtcNow;
        var ttl = _opt.Value.MagicLinkTtlMinutes;

        _db.MagicLinks.Add(new MagicLink
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            RouteId = cmd.RouteId,
            Email = email,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(ttl),
        });

        var route = await _db.PublicRoutes.FindAsync([cmd.RouteId], ct);
        var hostname = route?.Hostname ?? cmd.RouteId.ToString();
        var baseUrl = _opt.Value.PublicBaseUrl?.TrimEnd('/') ?? string.Empty;
        var url = $"{baseUrl}/auth/magic/{token}?routeId={cmd.RouteId}";

        await _db.SaveChangesAsync(ct);

        await _sender.SendAsync(email, url, hostname, ct);

        return Unit.Value;
    }
}
