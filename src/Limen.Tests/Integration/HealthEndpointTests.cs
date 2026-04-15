using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace Limen.Tests.Integration;

public sealed class HealthEndpointTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;

    public HealthEndpointTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(b => b.UseEnvironment("Testing"));
    }

    [Fact]
    public async Task Health_returns_200()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/healthz");
        res.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
