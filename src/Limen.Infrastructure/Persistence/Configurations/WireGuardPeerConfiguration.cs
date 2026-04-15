using Limen.Domain.Tunnels;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class WireGuardPeerConfiguration : IEntityTypeConfiguration<WireGuardPeer>
{
    public void Configure(EntityTypeBuilder<WireGuardPeer> b)
    {
        b.ToTable("wireguard_peers");
        b.HasKey(p => p.Id);
        b.Property(p => p.PublicKey).IsRequired().HasMaxLength(64);
        b.HasIndex(p => p.PublicKey).IsUnique();
        b.Property(p => p.TunnelIp).IsRequired().HasMaxLength(64);
        b.HasIndex(p => p.TunnelIp).IsUnique();
        b.HasIndex(p => p.AgentId);
    }
}
