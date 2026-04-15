using Limen.Domain.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> b)
    {
        b.ToTable("nodes");
        b.HasKey(n => n.Id);
        b.Property(n => n.Name).IsRequired().HasMaxLength(128);
        b.Property(n => n.Roles).HasColumnType("text[]");
        b.Property(n => n.Status).HasConversion<string>().HasMaxLength(32);
        b.HasOne(n => n.Agent).WithOne().HasForeignKey<Agent>(a => a.NodeId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(n => n.Status);
    }
}
