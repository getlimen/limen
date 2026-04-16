using System.Text.Json;
using Limen.Application.Commands.Auth;
using Limen.Application.Common.Interfaces;
using Limen.Application.Queries.Auth;
using Limen.Application.Services;
using Limen.Domain.Auth;
using Mediator;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Limen.API.Endpoints;

public static class ResourceAuthEndpoints
{
    public static IEndpointRouteBuilder MapResourceAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var grp = app.MapGroup("/auth");

        grp.MapPost("/login-password", async (
            HttpContext ctx,
            [FromBody] LoginWithPasswordRequest req,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(
                new LoginWithPasswordCommand(req.RouteId, req.Email, req.Password), ct);
            if (result is null)
            {
                return Results.Unauthorized();
            }

            ctx.Response.Cookies.Append("limen_session", result.Jwt, BuildCookie(result.ExpiresAt));
            return Results.Ok(new { ok = true, expiresAt = result.ExpiresAt, redirect = req.ReturnTo ?? "/" });
        });

        grp.MapPost("/magic-request", async (
            [FromBody] MagicRequestRequest req,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new InitiateMagicLinkCommand(req.RouteId, req.Email), ct);
            return Results.Ok(new { ok = true });
        });

        grp.MapGet("/magic/{token}", async (
            string token,
            Guid routeId,
            string? returnTo,
            HttpContext ctx,
            IMediator mediator,
            CancellationToken ct) =>
        {
            var result = await mediator.Send(new VerifyMagicLinkCommand(token, routeId), ct);
            if (result is null)
            {
                return Results.BadRequest("Invalid or expired magic link.");
            }

            ctx.Response.Cookies.Append("limen_session", result.Jwt, BuildCookie(result.ExpiresAt));
            return Results.Redirect(returnTo ?? "/");
        });

        grp.MapGet("/resource-oidc", (
            Guid routeId,
            string? returnTo,
            ILoggerFactory loggerFactory) =>
        {
            var logger = loggerFactory.CreateLogger("ResourceAuth");
            logger.LogWarning(
                "Resource-level OIDC requested for route {RouteId} but is not yet wired; returning 501.",
                routeId);
            return Results.Problem(
                statusCode: 501,
                detail: "Resource-level OIDC not yet wired");
        });

        // Anonymous: Ostiarius (the reverse proxy) polls this endpoint to build its revocation list.
        // For v1 this is acceptable because the data is not secret — it is a list of already-revoked JTIs
        // that are still within their expiry window, which Ostiarius needs to enforce locally.
        grp.MapGet("/revoked", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetRevokedTokensQuery(), ct))).AllowAnonymous();

        // Anonymous: the signing public key must be freely available so Ostiarius can verify JWTs.
        grp.MapGet("/public-key", async (IMediator mediator, CancellationToken ct) =>
            Results.Ok(await mediator.Send(new GetSigningPublicKeyQuery(), ct))).AllowAnonymous();

        grp.MapPost("/refresh", async (
            HttpContext ctx,
            IAppDbContext db,
            JwtBuilder jwtBuilder,
            IClock clock,
            CancellationToken ct) =>
        {
            var sessionCookie = ctx.Request.Cookies["limen_session"];
            if (string.IsNullOrEmpty(sessionCookie))
            {
                return Results.Unauthorized();
            }

            var parts = sessionCookie.Split('.');
            if (parts.Length != 3)
            {
                return Results.Unauthorized();
            }

            Dictionary<string, JsonElement>? claims;
            try
            {
                var payloadJson = Base64UrlDecode(parts[1]);
                claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
            }
            catch
            {
                return Results.Unauthorized();
            }

            if (claims is null
                || !claims.TryGetValue("jti", out var jtiEl)
                || !claims.TryGetValue("sub", out var subEl)
                || !claims.TryGetValue("routeId", out var routeIdEl)
                || !claims.TryGetValue("authMethod", out var authMethodEl)
                || !claims.TryGetValue("cookieScope", out var cookieScopeEl))
            {
                return Results.Unauthorized();
            }

            if (!Guid.TryParse(jtiEl.GetString(), out var jti)
                || !Guid.TryParse(routeIdEl.GetString(), out var routeId))
            {
                return Results.Unauthorized();
            }

            var email = subEl.GetString() ?? string.Empty;
            var token = await db.IssuedTokens.FindAsync([jti], ct);

            if (token is null || token.RevokedAt is not null || token.ExpiresAt <= clock.UtcNow)
            {
                return Results.Unauthorized();
            }

            var authMethod = authMethodEl.GetString() ?? "password";
            var cookieScope = cookieScopeEl.GetString() ?? "strict";
            var (newJwt, newJti, newExp) = jwtBuilder.Build(routeId, email, authMethod, cookieScope);

            db.IssuedTokens.Add(new IssuedToken
            {
                Id = newJti,
                Subject = email,
                RouteId = routeId,
                IssuedAt = clock.UtcNow,
                ExpiresAt = newExp,
            });

            await db.SaveChangesAsync(ct);
            ctx.Response.Cookies.Append("limen_session", newJwt, BuildCookie(newExp));
            return Results.Ok(new { ok = true, expiresAt = newExp });
        });

        grp.MapPost("/signout", async (HttpContext ctx, IMediator mediator, CancellationToken ct) =>
        {
            var sessionCookie = ctx.Request.Cookies["limen_session"];
            if (!string.IsNullOrEmpty(sessionCookie))
            {
                var parts = sessionCookie.Split('.');
                if (parts.Length == 3)
                {
                    try
                    {
                        var payloadJson = Base64UrlDecode(parts[1]);
                        var claims = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(payloadJson);
                        if (claims is not null
                            && claims.TryGetValue("jti", out var jtiEl)
                            && Guid.TryParse(jtiEl.GetString(), out var jti))
                        {
                            await mediator.Send(new RevokeTokenCommand(jti), ct);
                        }
                    }
                    catch
                    {
                        // swallow parse errors — still delete cookie
                    }
                }
            }

            ctx.Response.Cookies.Delete("limen_session");
            return Results.Ok();
        });

        grp.MapPost("/admin/revoke", async (
            [FromBody] RevokeRequest req,
            IMediator mediator,
            CancellationToken ct) =>
        {
            await mediator.Send(new RevokeTokenCommand(req.Jti), ct);
            return Results.Ok();
        }).RequireAuthorization();

        return app;
    }

    private static CookieOptions BuildCookie(DateTimeOffset exp) => new()
    {
        HttpOnly = true,
        Secure = true,
        SameSite = SameSiteMode.Lax,
        Path = "/",
        Expires = exp,
    };

    private static byte[] Base64UrlDecode(string input)
    {
        var padded = input.Replace('-', '+').Replace('_', '/');
        var mod = padded.Length % 4;
        if (mod == 2) { padded += "=="; }
        else if (mod == 3) { padded += "="; }
        return Convert.FromBase64String(padded);
    }

    private sealed record LoginWithPasswordRequest(Guid RouteId, string Email, string Password, string? ReturnTo);
    private sealed record MagicRequestRequest(Guid RouteId, string Email);
    private sealed record RevokeRequest(Guid Jti);
}
