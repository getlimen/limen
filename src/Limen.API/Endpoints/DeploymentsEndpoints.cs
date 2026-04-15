using Limen.Application.Commands.Deployments;
using Limen.Application.Queries.Deployments;
using Mediator;

namespace Limen.API.Endpoints;

public static class DeploymentsEndpoints
{
    public static IEndpointRouteBuilder MapDeploymentsEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/deployments").RequireAuthorization();

        grp.MapGet("/", async (Guid? serviceId, IMediator m, CancellationToken ct) =>
            Results.Ok(await m.Send(new ListDeploymentsQuery(serviceId), ct)));

        grp.MapGet("/{id:guid}/logs", async (Guid id, IMediator m, CancellationToken ct) =>
        {
            var logs = await m.Send(new GetDeploymentLogsQuery(id), ct);
            return logs is null ? Results.NotFound() : Results.Text(logs, "text/plain");
        });

        grp.MapPost("/", async (CreateDeploymentCommand cmd, IMediator m, CancellationToken ct) =>
            Results.Ok(new { id = await m.Send(cmd, ct) }));

        grp.MapPost("/{id:guid}/cancel", async (Guid id, IMediator m, CancellationToken ct) =>
        {
            await m.Send(new CancelDeploymentCommand(id), ct);
            return Results.NoContent();
        });

        return app;
    }
}
