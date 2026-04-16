using Limen.Domain.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class AllowlistedEmailConfiguration : IEntityTypeConfiguration<AllowlistedEmail>
{
    public void Configure(EntityTypeBuilder<AllowlistedEmail> builder)
    {
        builder.ToTable("allowlisted_emails");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).IsRequired().HasMaxLength(256);
        builder.Property(x => x.AddedAt).IsRequired();
        builder.HasIndex(x => new { x.RouteId, x.Email }).IsUnique();
        builder.HasIndex(x => x.RouteId);
    }
}
