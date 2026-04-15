using Limen.Application.Common.Interfaces;
using Limen.Contracts.ForculusHttp;
using Limen.Application.Common.Options;
using Mediator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Limen.Application.Queries.Tunnels;

public sealed record GetForculusConfigQuery() : IQuery<ConfigSnapshot>;

internal sealed class GetForculusConfigQueryHandler : IQueryHandler<GetForculusConfigQuery, ConfigSnapshot>
{
    private readonly IAppDbContext _db;
    private readonly IOptions<ForculusSettings> _settings;

    public GetForculusConfigQueryHandler(IAppDbContext db, IOptions<ForculusSettings> settings)
    {
        _db = db;
        _settings = settings;
    }

    public async ValueTask<ConfigSnapshot> Handle(GetForculusConfigQuery q, CancellationToken ct)
    {
        var peers = await _db.WireGuardPeers
            .Where(p => p.RevokedAt == null)
            .Select(p => new PeerSpec(p.PublicKey, p.TunnelIp))
            .ToListAsync(ct);
        return new ConfigSnapshot(
            ServerPrivateKey: _settings.Value.PrivateKey,
            ListenPort: _settings.Value.ListenPort,
            InterfaceAddress: _settings.Value.InterfaceAddress,
            Peers: peers);
    }
}
