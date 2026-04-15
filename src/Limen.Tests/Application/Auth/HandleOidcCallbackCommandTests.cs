using FluentAssertions;
using Limen.Application.Commands.Auth;
using Limen.Application.Common.Interfaces;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Limen.Tests.Application.Auth;

public sealed class HandleOidcCallbackCommandTests
{
    [Fact]
    public async Task Creates_AdminSession_and_returns_session_id()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var fixedNow = new DateTimeOffset(2026, 04, 14, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(fixedNow);

        var handler = new HandleOidcCallbackCommandHandler(db, clock);
        var result = await handler.Handle(
            new HandleOidcCallbackCommand("sub-123", "admin@example.com", "10.0.0.1", "Mozilla"),
            CancellationToken.None);

        result.Should().NotBe(Guid.Empty);
        var session = await db.AdminSessions.FindAsync(result);
        session.Should().NotBeNull();
        session!.Email.Should().Be("admin@example.com");
        session.ExpiresAt.Should().Be(fixedNow.AddHours(12));
    }
}
