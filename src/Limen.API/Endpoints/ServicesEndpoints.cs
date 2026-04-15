using Limen.Application.Commands.Services;
using Limen.Application.Queries.Services;
using Mediator;

namespace Limen.API.Endpoints;

public static class ServicesEndpoints
{
    public static IEndpointRouteBuilder MapServicesEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/services").RequireAuthorization();

        grp.MapGet("/", async (IMediator m, CancellationToken ct) =>
            Results.Ok(await m.Send(new ListServicesQuery(), ct)));

        grp.MapPost("/", async (CreateServiceCommand cmd, IMediator m, CancellationToken ct) =>
            Results.Ok(new { id = await m.Send(cmd, ct) }));

        return app;
    }
}
