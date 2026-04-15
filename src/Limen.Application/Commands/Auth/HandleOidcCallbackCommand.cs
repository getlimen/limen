using Limen.Application.Common.Interfaces;
using Limen.Domain.Auth;
using Mediator;

namespace Limen.Application.Commands.Auth;

public sealed record HandleOidcCallbackCommand(string Subject, string Email, string? IpAddress, string? UserAgent)
    : ICommand<Guid>;

internal sealed class HandleOidcCallbackCommandHandler : ICommandHandler<HandleOidcCallbackCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public HandleOidcCallbackCommandHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async ValueTask<Guid> Handle(HandleOidcCallbackCommand cmd, CancellationToken ct)
    {
        var now = _clock.UtcNow;
        var session = new AdminSession
        {
            Id = Guid.NewGuid(),
            Subject = cmd.Subject,
            Email = cmd.Email,
            IpAddress = cmd.IpAddress,
            UserAgent = cmd.UserAgent,
            CreatedAt = now,
            ExpiresAt = now.AddHours(12),
        };
        _db.AdminSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session.Id;
    }
}
