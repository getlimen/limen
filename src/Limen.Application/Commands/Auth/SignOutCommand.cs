using Limen.Application.Common.Interfaces;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Commands.Auth;

public sealed record SignOutCommand(Guid SessionId) : ICommand<Unit>;

internal sealed class SignOutCommandHandler : ICommandHandler<SignOutCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public SignOutCommandHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async ValueTask<Unit> Handle(SignOutCommand cmd, CancellationToken ct)
    {
        var session = await _db.AdminSessions.FirstOrDefaultAsync(s => s.Id == cmd.SessionId, ct);
        if (session is null)
        {
            return Unit.Value;
        }
        session.RevokedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
