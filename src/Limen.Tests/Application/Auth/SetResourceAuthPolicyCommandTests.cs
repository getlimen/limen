using FluentAssertions;
using Limen.Application.Commands.Auth;
using Limen.Application.Common.Interfaces;
using Limen.Domain.Auth;
using Limen.Domain.Routes;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Limen.Tests.Application.Auth;

public sealed class SetResourceAuthPolicyCommandTests
{
    private static (AppDbContext db, IClock clock, IPasswordHasher hasher, DateTimeOffset now) MakeContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);
        var hasher = Substitute.For<IPasswordHasher>();
        hasher.Hash(Arg.Any<string>()).Returns(ci => $"hashed:{ci.Arg<string>()}");
        return (db, clock, hasher, now);
    }

    private static SetResourceAuthPolicyCommandHandler MakeHandler(
        AppDbContext db, IClock clock, IPasswordHasher hasher)
        => new(db, clock, hasher);

    private static PublicRoute SeedRoute(AppDbContext db, Guid routeId)
    {
        var route = new PublicRoute
        {
            Id = routeId,
            ServiceId = Guid.NewGuid(),
            ProxyNodeId = Guid.NewGuid(),
            Hostname = "example.com",
            AuthPolicy = "none",
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.PublicRoutes.Add(route);
        db.SaveChanges();
        return route;
    }

    [Fact]
    public async Task Password_mode_hashes_password_and_stores_policy()
    {
        var (db, clock, hasher, _) = MakeContext();
        var routeId = Guid.NewGuid();
        SeedRoute(db, routeId);
        var handler = MakeHandler(db, clock, hasher);

        await handler.Handle(
            new SetResourceAuthPolicyCommand(routeId, "password", "secret", "strict", null),
            CancellationToken.None);

        var policy = await db.ResourceAuthPolicies.FirstAsync();
        policy.Mode.Should().Be("password");
        policy.PasswordHash.Should().Be("hashed:secret");
        policy.RouteId.Should().Be(routeId);
        hasher.Received(1).Hash("secret");
    }

    [Fact]
    public async Task Allowlist_mode_replaces_email_rows()
    {
        var (db, clock, hasher, now) = MakeContext();
        var routeId = Guid.NewGuid();
        SeedRoute(db, routeId);

        // Seed an existing email row that should be removed
        db.AllowlistedEmails.Add(new AllowlistedEmail
        {
            Id = Guid.NewGuid(),
            RouteId = routeId,
            Email = "old@example.com",
            AddedAt = now,
        });
        db.SaveChanges();

        var handler = MakeHandler(db, clock, hasher);

        await handler.Handle(
            new SetResourceAuthPolicyCommand(
                routeId, "allowlist", null, "strict",
                new[] { "Alice@EXAMPLE.COM", "bob@example.com" }),
            CancellationToken.None);

        var emails = await db.AllowlistedEmails
            .Where(e => e.RouteId == routeId)
            .Select(e => e.Email)
            .ToListAsync();

        emails.Should().BeEquivalentTo(new[] { "alice@example.com", "bob@example.com" });
        emails.Should().NotContain("old@example.com");
    }

    [Fact]
    public async Task PublicRoute_AuthPolicy_is_synced()
    {
        var (db, clock, hasher, _) = MakeContext();
        var routeId = Guid.NewGuid();
        SeedRoute(db, routeId);
        var handler = MakeHandler(db, clock, hasher);

        await handler.Handle(
            new SetResourceAuthPolicyCommand(routeId, "password", "pw", "strict", null),
            CancellationToken.None);

        var route = await db.PublicRoutes.FindAsync(routeId);
        route!.AuthPolicy.Should().Be("password");
    }

    [Fact]
    public async Task Upsert_updates_existing_policy()
    {
        var (db, clock, hasher, now) = MakeContext();
        var routeId = Guid.NewGuid();
        SeedRoute(db, routeId);

        db.ResourceAuthPolicies.Add(new ResourceAuthPolicy
        {
            Id = Guid.NewGuid(),
            RouteId = routeId,
            Mode = "none",
            CookieScope = "strict",
            CreatedAt = now,
            UpdatedAt = now,
        });
        db.SaveChanges();

        var handler = MakeHandler(db, clock, hasher);

        await handler.Handle(
            new SetResourceAuthPolicyCommand(routeId, "password", "newpw", "domain", null),
            CancellationToken.None);

        var count = await db.ResourceAuthPolicies.CountAsync();
        count.Should().Be(1);
        var policy = await db.ResourceAuthPolicies.FirstAsync();
        policy.Mode.Should().Be("password");
        policy.CookieScope.Should().Be("domain");
    }
}
