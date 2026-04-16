using Limen.Domain.Auth;
using Limen.Domain.Deployments;
using Limen.Domain.Nodes;
using Limen.Domain.Routes;
using Limen.Domain.Services;
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
    DbSet<Service> Services { get; }
    DbSet<PublicRoute> PublicRoutes { get; }
    DbSet<Deployment> Deployments { get; }
    DbSet<ResourceAuthPolicy> ResourceAuthPolicies { get; }
    DbSet<AllowlistedEmail> AllowlistedEmails { get; }
    DbSet<MagicLink> MagicLinks { get; }
    DbSet<IssuedToken> IssuedTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken ct);

    /// <summary>True when the underlying provider supports bulk operations like ExecuteUpdateAsync.</summary>
    bool SupportsBulkUpdate { get; }
}
