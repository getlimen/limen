using Limen.Domain.Deployments;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class DeploymentConfiguration : IEntityTypeConfiguration<Deployment>
{
    public void Configure(EntityTypeBuilder<Deployment> b)
    {
        b.ToTable("deployments");
        b.HasKey(d => d.Id);
        b.Property(d => d.ImageDigest).HasMaxLength(128);
        b.Property(d => d.ImageTag).HasMaxLength(256);
        b.Property(d => d.CurrentStage).HasMaxLength(256);
        b.Property(d => d.Status)
            .HasConversion<string>()
            .HasMaxLength(32);

        b.HasIndex(d => d.ServiceId);
        b.HasIndex(d => d.TargetNodeId);
        b.HasIndex(d => d.Status);

        b.HasIndex(d => new { d.ServiceId, d.ImageDigest })
            .IsUnique()
            .HasFilter("\"Status\" IN ('Queued','InProgress')");
    }
}
