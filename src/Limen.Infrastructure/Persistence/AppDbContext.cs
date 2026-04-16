using Limen.Application.Common.Interfaces;
using Limen.Domain.Auth;
using Limen.Domain.Deployments;
using Limen.Domain.Nodes;
using Limen.Domain.Routes;
using Limen.Domain.Services;
using Limen.Domain.Tunnels;
using Microsoft.EntityFrameworkCore;

namespace Limen.Infrastructure.Persistence;

public sealed class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AdminSession> AdminSessions => Set<AdminSession>();
    public DbSet<Node> Nodes => Set<Node>();
    public DbSet<Agent> Agents => Set<Agent>();
    public DbSet<ProvisioningKey> ProvisioningKeys => Set<ProvisioningKey>();
    public DbSet<WireGuardPeer> WireGuardPeers => Set<WireGuardPeer>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<PublicRoute> PublicRoutes => Set<PublicRoute>();
    public DbSet<Deployment> Deployments => Set<Deployment>();
    public DbSet<ResourceAuthPolicy> ResourceAuthPolicies => Set<ResourceAuthPolicy>();
    public DbSet<AllowlistedEmail> AllowlistedEmails => Set<AllowlistedEmail>();
    public DbSet<MagicLink> MagicLinks => Set<MagicLink>();
    public DbSet<IssuedToken> IssuedTokens => Set<IssuedToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken ct) => base.SaveChangesAsync(ct);

    bool IAppDbContext.SupportsBulkUpdate =>
        Database.ProviderName is not null
        && !Database.ProviderName.Contains("InMemory", StringComparison.OrdinalIgnoreCase);
}
