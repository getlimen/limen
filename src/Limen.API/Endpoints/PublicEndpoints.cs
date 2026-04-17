using Limen.Application.Queries.Auth;
using Mediator;

namespace Limen.API.Endpoints;

public static class PublicEndpoints
{
    public static IEndpointRouteBuilder MapPublicEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/public");

        grp.MapGet("/route-policy/{routeId:guid}", async (Guid routeId, IMediator m, CancellationToken ct) =>
        {
            var policy = await m.Send(new GetPublicRoutePolicyQuery(routeId), ct);
            return policy is null ? Results.NotFound() : Results.Ok(policy);
        });

        return app;
    }
}
