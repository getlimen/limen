using FluentAssertions;
using Limen.Domain.Auth;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Limen.Tests.Infrastructure.Persistence;

public sealed class AppDbContextTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:16-alpine")
        .Build();

    public async Task InitializeAsync() => await _pg.StartAsync();
    public async Task DisposeAsync() => await _pg.StopAsync();

    [Fact]
    public async Task Can_persist_and_read_AdminSession()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options;

        await using (var ctx = new AppDbContext(opts))
        {
            await ctx.Database.EnsureCreatedAsync();
            ctx.AdminSessions.Add(new AdminSession
            {
                Id = Guid.NewGuid(),
                Subject = "test-subject",
                Email = "admin@example.com",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(1),
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = new AppDbContext(opts))
        {
            var session = await ctx.AdminSessions.FirstAsync();
            session.Email.Should().Be("admin@example.com");
        }
    }
}
