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

public sealed class LoginWithPasswordCommandTests
{
    private static (AppDbContext db, IClock clock, IPasswordHasher hasher, JwtBuilder jwt, DateTimeOffset now)
        MakeContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 4, 15, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);

        var hasher = Substitute.For<IPasswordHasher>();
        var signer = Substitute.For<ITokenSigner>();
        signer.KeyId.Returns("test-key");
        signer.SignJwt(Arg.Any<IDictionary<string, object>>(), Arg.Any<IDictionary<string, object>>())
            .Returns(ci =>
            {
                var payload = ci.ArgAt<IDictionary<string, object>>(1);
                var jtiStr = payload["jti"].ToString()!;
                return $"header.{Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(
                    System.Text.Json.JsonSerializer.Serialize(payload)))}.sig";
            });

        var settings = Options.Create(new AuthSettings { TokenTtlMinutes = 15 });
        var jwt = new JwtBuilder(signer, clock, settings);
        return (db, clock, hasher, jwt, now);
    }

    private static LoginWithPasswordCommandHandler MakeHandler(
        AppDbContext db, IClock clock, IPasswordHasher hasher, JwtBuilder jwt)
        => new(db, clock, hasher, jwt);

    private static ResourceAuthPolicy SeedPasswordPolicy(AppDbContext db, Guid routeId, DateTimeOffset now)
    {
        var policy = new ResourceAuthPolicy
        {
            Id = Guid.NewGuid(),
            RouteId = routeId,
            Mode = "password",
            PasswordHash = "hashed:correct",
            CookieScope = "strict",
            CreatedAt = now,
            UpdatedAt = now,
        };
        db.ResourceAuthPolicies.Add(policy);
        db.SaveChanges();
        return policy;
    }

    [Fact]
    public async Task Success_returns_jwt_and_persists_issued_token()
    {
        var (db, clock, hasher, jwt, now) = MakeContext();
        var routeId = Guid.NewGuid();
        SeedPasswordPolicy(db, routeId, now);
        hasher.Verify("correct", "hashed:correct").Returns(true);

        var handler = MakeHandler(db, clock, hasher, jwt);
        var result = await handler.Handle(
            new LoginWithPasswordCommand(routeId, "user@example.com", "correct"),
            CancellationToken.None);

        result.Should().NotBeNull();
        result!.Jwt.Should().NotBeEmpty();
        result.ExpiresAt.Should().BeAfter(now);

        var tokenCount = await db.IssuedTokens.CountAsync();
        tokenCount.Should().Be(1);
        var token = await db.IssuedTokens.FirstAsync();
        token.Subject.Should().Be("user@example.com");
        token.RouteId.Should().Be(routeId);
    }

    [Fact]
    public async Task Wrong_password_returns_null()
    {
        var (db, clock, hasher, jwt, now) = MakeContext();
        var routeId = Guid.NewGuid();
        SeedPasswordPolicy(db, routeId, now);
        hasher.Verify("wrong", "hashed:correct").Returns(false);

        var handler = MakeHandler(db, clock, hasher, jwt);
        var result = await handler.Handle(
            new LoginWithPasswordCommand(routeId, "user@example.com", "wrong"),
            CancellationToken.None);

        result.Should().BeNull();
        (await db.IssuedTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task Wrong_mode_returns_null()
    {
        var (db, clock, hasher, jwt, now) = MakeContext();
        var routeId = Guid.NewGuid();

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

        var handler = MakeHandler(db, clock, hasher, jwt);
        var result = await handler.Handle(
            new LoginWithPasswordCommand(routeId, "user@example.com", "anything"),
            CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task No_policy_returns_null()
    {
        var (db, clock, hasher, jwt, _) = MakeContext();
        var handler = MakeHandler(db, clock, hasher, jwt);
        var result = await handler.Handle(
            new LoginWithPasswordCommand(Guid.NewGuid(), "user@example.com", "pw"),
            CancellationToken.None);
        result.Should().BeNull();
    }
}
