using Limen.Application.Commands.Routes;
using Limen.Application.Queries.Routes;
using Mediator;

namespace Limen.API.Endpoints;

public static class RoutesEndpoints
{
    public static IEndpointRouteBuilder MapRoutesEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/routes").RequireAuthorization();

        grp.MapGet("/", async (IMediator m, CancellationToken ct) =>
            Results.Ok(await m.Send(new ListRoutesQuery(), ct)));

        grp.MapPost("/", async (AddRouteCommand cmd, IMediator m, CancellationToken ct) =>
            Results.Ok(new { id = await m.Send(cmd, ct) }));

        return app;
    }
}
