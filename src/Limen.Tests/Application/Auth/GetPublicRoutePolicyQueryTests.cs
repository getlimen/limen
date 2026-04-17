using FluentAssertions;
using Limen.Application.Queries.Auth;
using Limen.Domain.Routes;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Limen.Tests.Application.Auth;

public sealed class GetPublicRoutePolicyQueryTests
{
    private static AppDbContext MakeDb()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        return new AppDbContext(opts);
    }

    [Fact]
    public async Task Returns_dto_for_known_route()
    {
        var db = MakeDb();
        var routeId = Guid.NewGuid();
        db.PublicRoutes.Add(new PublicRoute
        {
            Id = routeId,
            ServiceId = Guid.NewGuid(),
            ProxyNodeId = Guid.NewGuid(),
            Hostname = "app.example.com",
            TlsEnabled = true,
            AuthPolicy = "password",
            CreatedAt = DateTimeOffset.UtcNow,
        });
        db.SaveChanges();

        var handler = new GetPublicRoutePolicyQueryHandler(db);
        var result = await handler.Handle(new GetPublicRoutePolicyQuery(routeId), CancellationToken.None);

        result.Should().NotBeNull();
        result!.RouteId.Should().Be(routeId);
        result.Mode.Should().Be("password");
        result.Hostname.Should().Be("app.example.com");
    }

    [Fact]
    public async Task Returns_null_for_unknown_route()
    {
        var db = MakeDb();

        var handler = new GetPublicRoutePolicyQueryHandler(db);
        var result = await handler.Handle(new GetPublicRoutePolicyQuery(Guid.NewGuid()), CancellationToken.None);

        result.Should().BeNull();
    }
}
