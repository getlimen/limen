using FluentAssertions;
using Limen.Application.Common.Interfaces;
using Limen.Application.Queries.Auth;
using Limen.Domain.Auth;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Limen.Tests.Application.Auth;

public sealed class GetRevokedTokensQueryTests
{
    private static (AppDbContext db, IClock clock, DateTimeOffset now) MakeContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);
        return (db, clock, now);
    }

    [Fact]
    public async Task Returns_only_revoked_and_not_yet_expired_tokens()
    {
        var (db, clock, now) = MakeContext();
        var routeId = Guid.NewGuid();

        // Revoked and still valid (should appear)
        db.IssuedTokens.Add(new IssuedToken
        {
            Id = Guid.NewGuid(),
            Subject = "a@test.com",
            RouteId = routeId,
            IssuedAt = now.AddMinutes(-10),
            ExpiresAt = now.AddMinutes(5),
            RevokedAt = now.AddMinutes(-2),
        });

        // Not revoked (should not appear)
        db.IssuedTokens.Add(new IssuedToken
        {
            Id = Guid.NewGuid(),
            Subject = "b@test.com",
            RouteId = routeId,
            IssuedAt = now.AddMinutes(-10),
            ExpiresAt = now.AddMinutes(5),
            RevokedAt = null,
        });

        // Revoked but already expired (should not appear)
        db.IssuedTokens.Add(new IssuedToken
        {
            Id = Guid.NewGuid(),
            Subject = "c@test.com",
            RouteId = routeId,
            IssuedAt = now.AddMinutes(-20),
            ExpiresAt = now.AddMinutes(-5), // expired
            RevokedAt = now.AddMinutes(-15),
        });

        db.SaveChanges();

        var handler = new GetRevokedTokensQueryHandler(db, clock);
        var result = await handler.Handle(new GetRevokedTokensQuery(), CancellationToken.None);

        result.Should().HaveCount(1);
        result[0].Jti.Should().Be((await db.IssuedTokens.FirstAsync(t => t.Subject == "a@test.com")).Id);
    }

    [Fact]
    public async Task Returns_empty_list_when_no_revoked_tokens()
    {
        var (db, clock, now) = MakeContext();
        var routeId = Guid.NewGuid();

        db.IssuedTokens.Add(new IssuedToken
        {
            Id = Guid.NewGuid(),
            Subject = "a@test.com",
            RouteId = routeId,
            IssuedAt = now,
            ExpiresAt = now.AddMinutes(15),
            RevokedAt = null,
        });
        db.SaveChanges();

        var handler = new GetRevokedTokensQueryHandler(db, clock);
        var result = await handler.Handle(new GetRevokedTokensQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }
}
