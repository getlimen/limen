using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using Limen.Application.Commands.Nodes;
using Limen.Application.Common.Interfaces;
using Limen.Domain.Nodes;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Limen.Tests.Application.Nodes;

public sealed class EnrollAgentCommandTests
{
    private static (AppDbContext db, IClock clock, DateTimeOffset now) MakeContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 04, 14, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);
        return (db, clock, now);
    }

    private static (string plaintext, ProvisioningKey row) SeedKey(AppDbContext db, DateTimeOffset now, TimeSpan? ttl = null, DateTimeOffset? usedAt = null)
    {
        var plaintext = "test-plaintext-key-with-enough-entropy-abcdef";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));
        var pk = new ProvisioningKey
        {
            Id = Guid.NewGuid(),
            KeyHash = hash,
            IntendedRoles = new[] { "docker" },
            CreatedAt = now.AddMinutes(-1),
            ExpiresAt = now + (ttl ?? TimeSpan.FromMinutes(15)),
            UsedAt = usedAt,
        };
        db.ProvisioningKeys.Add(pk);
        db.SaveChanges();
        return (plaintext, pk);
    }

    [Fact]
    public async Task Creates_node_and_agent_and_burns_key()
    {
        var (db, clock, now) = MakeContext();
        var (plaintext, _) = SeedKey(db, now);
        var handler = new EnrollAgentCommandHandler(db, clock);

        var result = await handler.Handle(
            new EnrollAgentCommand(plaintext, "host-1", new[] { "docker" }, "linux-x64", "0.1.0"),
            CancellationToken.None);

        result.AgentId.Should().NotBe(Guid.Empty);
        result.Secret.Should().NotBeNullOrWhiteSpace();
        (await db.Nodes.CountAsync()).Should().Be(1);
        (await db.Agents.CountAsync()).Should().Be(1);
        var key = await db.ProvisioningKeys.FirstAsync();
        key.UsedAt.Should().Be(now);
        key.ResultingNodeId.Should().NotBeNull();
    }

    [Fact]
    public async Task Throws_on_expired_key()
    {
        var (db, clock, now) = MakeContext();
        var (plaintext, _) = SeedKey(db, now, ttl: TimeSpan.FromMinutes(-1));
        var handler = new EnrollAgentCommandHandler(db, clock);

        var act = async () => await handler.Handle(
            new EnrollAgentCommand(plaintext, "host-1", new[] { "docker" }, "linux-x64", "0.1.0"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*expired*");
    }

    [Fact]
    public async Task Throws_on_already_used_key()
    {
        var (db, clock, now) = MakeContext();
        var (plaintext, _) = SeedKey(db, now, usedAt: now.AddSeconds(-30));
        var handler = new EnrollAgentCommandHandler(db, clock);

        var act = async () => await handler.Handle(
            new EnrollAgentCommand(plaintext, "host-1", new[] { "docker" }, "linux-x64", "0.1.0"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*used*");
    }

    [Fact]
    public async Task Throws_on_unknown_key()
    {
        var (db, clock, _) = MakeContext();
        var handler = new EnrollAgentCommandHandler(db, clock);

        var act = async () => await handler.Handle(
            new EnrollAgentCommand("random-nonexistent-key", "host-1", new[] { "docker" }, "linux-x64", "0.1.0"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Invalid*");
    }
}
