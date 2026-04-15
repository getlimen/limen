using Limen.Domain.Routes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class PublicRouteConfiguration : IEntityTypeConfiguration<PublicRoute>
{
    public void Configure(EntityTypeBuilder<PublicRoute> b)
    {
        b.ToTable("public_routes");
        b.HasKey(r => r.Id);
        b.Property(r => r.Hostname).IsRequired().HasMaxLength(256);
        b.Property(r => r.AuthPolicy).HasMaxLength(32);
        b.HasIndex(r => r.Hostname).IsUnique();
        b.HasIndex(r => r.ProxyNodeId);
        b.HasIndex(r => r.ServiceId);
    }
}
