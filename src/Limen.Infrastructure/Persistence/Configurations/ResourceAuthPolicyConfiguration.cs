using Limen.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class ResourceAuthPolicyConfiguration : IEntityTypeConfiguration<ResourceAuthPolicy>
{
    public void Configure(EntityTypeBuilder<ResourceAuthPolicy> builder)
    {
        builder.ToTable("resource_auth_policies");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Mode).IsRequired().HasMaxLength(32);
        builder.Property(x => x.CookieScope).IsRequired().HasMaxLength(32);
        builder.Property(x => x.PasswordHash).HasMaxLength(512);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
        builder.HasIndex(x => x.RouteId).IsUnique();
    }
}
