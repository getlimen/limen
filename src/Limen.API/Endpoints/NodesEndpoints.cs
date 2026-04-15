using Limen.Application.Commands.Nodes;
using Limen.Application.Queries.Nodes;
using Mediator;

namespace Limen.API.Endpoints;

public sealed record CreateProvisioningKeyRequest(string[] Roles);

public static class NodesEndpoints
{
    public static IEndpointRouteBuilder MapNodesEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/nodes").RequireAuthorization();

        grp.MapGet("/", async (IMediator m, CancellationToken ct) =>
            Results.Ok(await m.Send(new ListNodesQuery(), ct)));

        grp.MapPost("/provisioning-keys", async (CreateProvisioningKeyRequest req, IMediator m, CancellationToken ct) =>
            Results.Ok(await m.Send(new CreateProvisioningKeyCommand(req.Roles), ct)));

        return app;
    }
}
