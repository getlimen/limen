using Limen.Application.Commands.Auth;
using Limen.Application.Commands.Routes;
using Limen.Application.Queries.Auth;
using Limen.Application.Queries.Routes;
using Mediator;

namespace Limen.API.Endpoints;

file sealed record AuthPolicyRequest(
    string Mode,
    string? Password,
    string CookieScope,
    string[]? AllowedEmails);

public static class RoutesEndpoints
{
    public static IEndpointRouteBuilder MapRoutesEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/api/routes").RequireAuthorization();

        grp.MapGet("/", async (IMediator m, CancellationToken ct) =>
            Results.Ok(await m.Send(new ListRoutesQuery(), ct)));

        grp.MapPost("/", async (AddRouteCommand cmd, IMediator m, CancellationToken ct) =>
            Results.Ok(new { id = await m.Send(cmd, ct) }));

        grp.MapGet("/{routeId}/auth-policy", async (Guid routeId, IMediator m, CancellationToken ct) =>
        {
            var dto = await m.Send(new GetResourceAuthPolicyQuery(routeId), ct);
            return dto is null ? Results.NotFound() : Results.Ok(dto);
        });

        grp.MapPost("/{routeId}/auth-policy", async (
            Guid routeId,
            AuthPolicyRequest body,
            IMediator m,
            CancellationToken ct) =>
        {
            await m.Send(new SetResourceAuthPolicyCommand(
                routeId,
                body.Mode,
                body.Password,
                body.CookieScope,
                body.AllowedEmails), ct);
            return Results.Ok();
        }).RequireAuthorization();

        return app;
    }
}
