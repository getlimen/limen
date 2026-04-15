using Limen.Application.Common.Interfaces;
using Limen.Application.Services;
using Limen.Domain.Routes;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Commands.Routes;

public sealed record AddRouteCommand(
    Guid ServiceId,
    Guid ProxyNodeId,
    string Hostname,
    bool TlsEnabled,
    string AuthPolicy) : ICommand<Guid>;

internal sealed class AddRouteCommandHandler : ICommandHandler<AddRouteCommand, Guid>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;
    private readonly ProxyConfigPusher _pusher;

    public AddRouteCommandHandler(IAppDbContext db, IClock clock, ProxyConfigPusher pusher)
    { _db = db; _clock = clock; _pusher = pusher; }

    public async ValueTask<Guid> Handle(AddRouteCommand cmd, CancellationToken ct)
    {
        var serviceExists = await _db.Services.AnyAsync(s => s.Id == cmd.ServiceId, ct);
        if (!serviceExists)
        {
            throw new InvalidOperationException($"Service {cmd.ServiceId} not found.");
        }

        var route = new PublicRoute
        {
            Id = Guid.NewGuid(),
            ServiceId = cmd.ServiceId,
            ProxyNodeId = cmd.ProxyNodeId,
            Hostname = cmd.Hostname,
            TlsEnabled = cmd.TlsEnabled,
            AuthPolicy = cmd.AuthPolicy,
            CreatedAt = _clock.UtcNow,
        };
        _db.PublicRoutes.Add(route);
        await _db.SaveChangesAsync(ct);

        try { await _pusher.PushFullAsync(cmd.ProxyNodeId, ct); }
        catch { /* reconciled later when proxy connects */ }

        return route.Id;
    }
}
