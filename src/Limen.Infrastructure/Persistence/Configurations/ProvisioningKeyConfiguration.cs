using Limen.Domain.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class ProvisioningKeyConfiguration : IEntityTypeConfiguration<ProvisioningKey>
{
    public void Configure(EntityTypeBuilder<ProvisioningKey> b)
    {
        b.ToTable("provisioning_keys");
        b.HasKey(x => x.Id);
        b.Property(x => x.KeyHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.IntendedRoles).HasColumnType("text[]");
        b.HasIndex(x => x.KeyHash).IsUnique();
        b.HasIndex(x => x.ExpiresAt);
    }
}
