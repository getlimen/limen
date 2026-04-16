using Limen.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class IssuedTokenConfiguration : IEntityTypeConfiguration<IssuedToken>
{
    public void Configure(EntityTypeBuilder<IssuedToken> builder)
    {
        builder.ToTable("issued_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Subject).IsRequired().HasMaxLength(256);
        builder.Property(x => x.IssuedAt).IsRequired();
        builder.Property(x => x.ExpiresAt).IsRequired();
        builder.HasIndex(x => x.ExpiresAt);
        builder.HasIndex(x => x.RevokedAt).HasFilter("\"RevokedAt\" IS NOT NULL");
        builder.HasIndex(x => x.Subject);
    }
}
