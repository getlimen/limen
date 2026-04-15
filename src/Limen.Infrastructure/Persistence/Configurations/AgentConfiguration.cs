using Limen.Domain.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> b)
    {
        b.ToTable("agents");
        b.HasKey(a => a.Id);
        b.Property(a => a.SecretHash).IsRequired();
        b.Property(a => a.Hostname).HasMaxLength(256);
        b.Property(a => a.Platform).HasMaxLength(64);
        b.Property(a => a.AgentVersion).HasMaxLength(32);
    }
}
