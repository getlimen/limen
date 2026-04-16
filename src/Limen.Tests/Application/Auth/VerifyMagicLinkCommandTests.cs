using System.Security.Cryptography;
using FluentAssertions;
using Limen.Application.Commands.Auth;
using Limen.Application.Common.Interfaces;
using Limen.Application.Common.Options;
using Limen.Application.Services;
using Limen.Domain.Auth;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using NSubstitute;

namespace Limen.Tests.Application.Auth;

public sealed class VerifyMagicLinkCommandTests
{
    private static (AppDbContext db, IClock clock, JwtBuilder jwt, DateTimeOffset now) MakeContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);

        var signer = Substitute.For<ITokenSigner>();
        signer.KeyId.Returns("test-key");
        signer.SignJwt(Arg.Any<IDictionary<string, object>>(), Arg.Any<IDictionary<string, object>>())
            .Returns("test.jwt.sig");

        var settings = Options.Create(new AuthSettings { TokenTtlMinutes = 15 });
        var jwt = new JwtBuilder(signer, clock, settings);
        return (db, clock, jwt, now);
    }

    private static VerifyMagicLinkCommandHandler MakeHandler(AppDbContext db, IClock clock, JwtBuilder jwt)
        => new(db, clock, jwt);

    private static (string plainToken, MagicLink link) SeedMagicLink(
        AppDbContext db, Guid routeId, DateTimeOffset now, bool used = false, bool expired = false)
    {
        var tokenBytes = new byte[32];
        Random.Shared.NextBytes(tokenBytes);
        var token = Convert.ToBase64String(tokenBytes)
            .TrimEnd('=').Replace('+', '-').Replace('/', '_');

        var hashBytes = SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        var tokenHash = Convert.ToBase64String(hashBytes);

        var link = new MagicLink
        {
            Id = Guid.NewGuid(),
            TokenHash = tokenHash,
            RouteId = routeId,
            Email = "user@example.com",
            CreatedAt = now,
            ExpiresAt = expired ? now.AddMinutes(-1) : now.AddMinutes(15),
            UsedAt = used ? now.AddMinutes(-1) : null,
        };
        db.MagicLinks.Add(link);

        db.ResourceAuthPolicies.Add(new ResourceAuthPolicy
        {
            Id = Guid.NewGuid(),
            RouteId = routeId,
            Mode = "allowlist",
            CookieScope = "strict",
            CreatedAt = now,
            UpdatedAt = now,
        });

        db.SaveChanges();
        return (token, link);
    }

    [Fact]
    public async Task Success_returns_jwt_and_marks_used()
    {
        var (db, clock, jwt, now) = MakeContext();
        var routeId = Guid.NewGuid();
        var (token, _) = SeedMagicLink(db, routeId, now);
        var handler = MakeHandler(db, clock, jwt);

        var result = await handler.Handle(
            new VerifyMagicLinkCommand(token, routeId),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.RouteId.Should().Be(routeId);
        result.Jwt.Should().NotBeEmpty();

        var link = await db.MagicLinks.FirstAsync();
        link.UsedAt.Should().NotBeNull();

        (await db.IssuedTokens.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Reuse_fails_returns_null()
    {
        var (db, clock, jwt, now) = MakeContext();
        var routeId = Guid.NewGuid();
        var (token, _) = SeedMagicLink(db, routeId, now, used: true);
        var handler = MakeHandler(db, clock, jwt);

        var result = await handler.Handle(
            new VerifyMagicLinkCommand(token, routeId),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Expired_link_returns_null()
    {
        var (db, clock, jwt, now) = MakeContext();
        var routeId = Guid.NewGuid();
        var (token, _) = SeedMagicLink(db, routeId, now, expired: true);
        var handler = MakeHandler(db, clock, jwt);

        var result = await handler.Handle(
            new VerifyMagicLinkCommand(token, routeId),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Wrong_routeId_returns_null()
    {
        var (db, clock, jwt, now) = MakeContext();
        var routeId = Guid.NewGuid();
        var (token, _) = SeedMagicLink(db, routeId, now);
        var handler = MakeHandler(db, clock, jwt);

        var result = await handler.Handle(
            new VerifyMagicLinkCommand(token, Guid.NewGuid()), // different routeId
            CancellationToken.None);

        result.Should().BeNull();
    }
}
