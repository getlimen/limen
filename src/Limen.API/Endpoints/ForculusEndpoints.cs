using Limen.Application.Queries.Tunnels;
using Mediator;

namespace Limen.API.Endpoints;

public static class ForculusEndpoints
{
    public static IEndpointRouteBuilder MapForculusEndpoints(this IEndpointRouteBuilder app)
    {
        // No auth for now — forculus runs inside the docker network; not exposed publicly.
        // Plan 03 follow-up can add a shared-secret header if needed.
        app.MapGet("/api/forculus/config", async (IMediator m, CancellationToken ct) =>
            Results.Ok(await m.Send(new GetForculusConfigQuery(), ct)));
        return app;
    }
}
