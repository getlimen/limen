using Limen.Application.Common.Interfaces;
using Limen.Contracts.ProxyMessages;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Services;

public sealed class ProxyConfigPusher
{
    private readonly IAppDbContext _db;
    private readonly IProxyConnectionRegistry _registry;

    public ProxyConfigPusher(IAppDbContext db, IProxyConnectionRegistry registry)
    { _db = db; _registry = registry; }

    public async Task PushFullAsync(Guid proxyNodeId, CancellationToken ct)
    {
        // Pull all routes for this proxy, join with service to get the container + port,
        // then join with WireGuardPeer for the agent hosting the service to build the upstream URL.
        var rows = await (
            from r in _db.PublicRoutes
            where r.ProxyNodeId == proxyNodeId
            join s in _db.Services on r.ServiceId equals s.Id
            join a in _db.Agents on s.TargetNodeId equals a.NodeId
            join p in _db.WireGuardPeers on a.Id equals p.AgentId
            where p.RevokedAt == null
            select new { r, s, p }
        ).ToListAsync(ct);

        var specs = rows.Select(x =>
        {
            var tunnelIp = x.p.TunnelIp.Split('/')[0];
            var upstream = $"http://{tunnelIp}:{x.s.InternalPort}";
            return new RouteSpec(x.r.Id, x.r.Hostname, upstream, x.r.TlsEnabled, x.r.AuthPolicy);
        }).ToList();

        var channel = _registry.Get(proxyNodeId);
        if (channel is null)
        {
            return;
        }

        await channel.SendJsonAsync(ProxyMessageTypes.ApplyRouteSet, new ApplyRouteSet(specs), ct);
    }
}
