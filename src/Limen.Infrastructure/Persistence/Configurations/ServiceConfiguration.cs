using Limen.Domain.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class ServiceConfiguration : IEntityTypeConfiguration<Service>
{
    public void Configure(EntityTypeBuilder<Service> b)
    {
        b.ToTable("services");
        b.HasKey(s => s.Id);
        b.Property(s => s.Name).IsRequired().HasMaxLength(128);
        b.Property(s => s.ContainerName).HasMaxLength(128);
        b.Property(s => s.Image).HasMaxLength(512);
        b.HasIndex(s => s.Name);
        b.HasIndex(s => s.TargetNodeId);
    }
}
