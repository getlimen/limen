using Limen.Application.Common.Interfaces;
using Mediator;

namespace Limen.Application.Commands.Auth;

public sealed record RevokeTokenCommand(Guid Jti) : ICommand<Unit>;

internal sealed class RevokeTokenCommandHandler : ICommandHandler<RevokeTokenCommand, Unit>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public RevokeTokenCommandHandler(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async ValueTask<Unit> Handle(RevokeTokenCommand cmd, CancellationToken ct)
    {
        var token = await _db.IssuedTokens.FindAsync([cmd.Jti], ct);
        if (token is null)
        {
            return Unit.Value; // idempotent
        }

        token.RevokedAt = _clock.UtcNow;
        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
