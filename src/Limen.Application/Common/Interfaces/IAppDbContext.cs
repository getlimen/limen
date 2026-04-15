using Limen.Domain.Auth;
using Limen.Domain.Nodes;
using Limen.Domain.Tunnels;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Common.Interfaces;

public interface IAppDbContext
{
    DbSet<AdminSession> AdminSessions { get; }
    DbSet<Node> Nodes { get; }
    DbSet<Agent> Agents { get; }
    DbSet<ProvisioningKey> ProvisioningKeys { get; }
    DbSet<WireGuardPeer> WireGuardPeers { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);
}
