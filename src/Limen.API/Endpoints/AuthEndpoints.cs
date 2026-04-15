using System.Security.Claims;
using Limen.Application.Commands.Auth;
using Limen.Application.Queries.Auth;
using Mediator;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Limen.API.Endpoints;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/auth");

        grp.MapGet("/login", (HttpContext ctx) =>
            Results.Challenge(new AuthenticationProperties { RedirectUri = "/auth/complete" }, new[] { "oidc" }));

        grp.MapGet("/complete", async (HttpContext ctx, IMediator mediator) =>
        {
            var sub = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? ctx.User.FindFirstValue("sub") ?? "";
            var email = ctx.User.FindFirstValue(ClaimTypes.Email) ?? "";
            var ip = ctx.Connection.RemoteIpAddress?.ToString();
            var ua = ctx.Request.Headers.UserAgent.ToString();

            var sessionId = await mediator.Send(new HandleOidcCallbackCommand(sub, email, ip, ua));
            ctx.Response.Cookies.Append("limen_admin", sessionId.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Lax,
                Expires = DateTimeOffset.UtcNow.AddHours(12)
            });
            return Results.Redirect("/");
        }).RequireAuthorization();

        grp.MapPost("/signout", async (HttpContext ctx, IMediator mediator) =>
        {
            if (Guid.TryParse(ctx.Request.Cookies["limen_admin"], out var id))
            {
                await mediator.Send(new SignOutCommand(id));
            }

            ctx.Response.Cookies.Delete("limen_admin");
            await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return Results.Ok();
        });

        grp.MapGet("/me", async (HttpContext ctx, IMediator mediator) =>
        {
            if (!Guid.TryParse(ctx.Request.Cookies["limen_admin"], out var id))
            {
                return Results.Unauthorized();
            }

            var admin = await mediator.Send(new GetCurrentAdminQuery(id));
            return admin is null ? Results.Unauthorized() : Results.Ok(admin);
        });

        return app;
    }
}
