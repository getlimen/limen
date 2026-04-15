using FluentAssertions;
using Limen.Application.Commands.Nodes;
using Limen.Application.Common.Interfaces;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Limen.Tests.Application.Nodes;

public sealed class CreateProvisioningKeyCommandTests
{
    [Fact]
    public async Task Creates_one_shot_key_with_15min_TTL()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 04, 14, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);

        var handler = new CreateProvisioningKeyCommandHandler(db, clock);
        var result = await handler.Handle(new CreateProvisioningKeyCommand(new[] { "docker" }), CancellationToken.None);

        result.PlaintextKey.Should().NotBeNullOrWhiteSpace();
        result.PlaintextKey.Length.Should().BeGreaterThan(32);
        var stored = await db.ProvisioningKeys.FirstAsync();
        stored.ExpiresAt.Should().Be(now.AddMinutes(15));
        stored.IntendedRoles.Should().Contain("docker");
        stored.UsedAt.Should().BeNull();
    }
}
