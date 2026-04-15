using Limen.Application.Commands.Deployments;
using Limen.Application.Queries.Deployments;
using Mediator;

namespace Limen.API.Endpoints;

/// <summary>Request DTO for creating a deployment via the REST API.</summary>
/// <remarks>
/// PreviousDeploymentId is intentionally excluded; it is set only by the internal RegistryPollJob.
/// </remarks>
public sealed record CreateDeploymentRequest(Guid ServiceId, string ImageDigest, string ImageTag);

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

        grp.MapPost("/", async (CreateDeploymentRequest req, IMediator m, CancellationToken ct) =>
            Results.Ok(new { id = await m.Send(new CreateDeploymentCommand(req.ServiceId, req.ImageDigest, req.ImageTag, PreviousDeploymentId: null), ct) }));

        grp.MapPost("/{id:guid}/cancel", async (Guid id, IMediator m, CancellationToken ct) =>
        {
            await m.Send(new CancelDeploymentCommand(id), ct);
            return Results.NoContent();
        });

        return app;
    }
}
