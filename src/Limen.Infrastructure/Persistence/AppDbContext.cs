using Limen.Application.Common.Interfaces;
using Limen.Domain.Auth;
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    Task<int> IAppDbContext.SaveChangesAsync(CancellationToken ct) => base.SaveChangesAsync(ct);
}
