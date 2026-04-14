# Plan 06 — Resource-level authentication

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans`.

**Goal:** Implement Pangolin's killer feature: put an auth wall (`password` / `sso` / `allowlist`) in front of any Route. Ostiarius verifies short-lived Ed25519-signed JWTs locally (no round-trip per request). Limen handles login flows and issues tokens. Sub-minute revocation via poll.

**Prerequisites:** Plans 1-5 complete.

---

## File structure

**`limen`:**
- `src/Limen.Domain/Auth/{ResourceAuthPolicy.cs, AllowlistedEmail.cs, MagicLink.cs, IssuedToken.cs}`
- `src/Limen.Application/Commands/Auth/{LoginWithPasswordCommand.cs, InitiateMagicLinkCommand.cs, VerifyMagicLinkCommand.cs, InitiateResourceOidcCommand.cs, HandleResourceOidcCallbackCommand.cs, RevokeTokenCommand.cs, SetResourceAuthPolicyCommand.cs}`
- `src/Limen.Application/Queries/Auth/{GetRevokedTokensQuery.cs, GetSigningPublicKeyQuery.cs}`
- `src/Limen.Application/Services/TokenSigner.cs`
- `src/Limen.Infrastructure/Auth/{Ed25519TokenSigner.cs, EmailSender.cs}`
- `src/Limen.API/Endpoints/ResourceAuthEndpoints.cs` — `/auth/login`, `/auth/magic/{token}`, `/auth/refresh`, `/auth/revoked`, `/auth/public-key`
- Frontend: `src/Limen.Frontend/src/app/features/auth/{login.component.ts, magic-verify.component.ts}`

**`ostiarius`:**
- `src/Ostiarius.Application/Services/SessionVerifier.cs`
- `src/Ostiarius.Infrastructure/Auth/{Ed25519Verifier.cs, RevokedTokenPoller.cs}`
- `src/Ostiarius.Infrastructure/Proxy/AuthMiddleware.cs` (runs BEFORE YARP routing)

---

## Tasks

### Task 1: Domain + migration

```csharp
// src/Limen.Domain/Auth/ResourceAuthPolicy.cs
public class ResourceAuthPolicy
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }       // PublicRoute
    public string Mode { get; set; } = "none"; // none | password | sso | allowlist
    public string? PasswordHash { get; set; }  // argon2id, if mode=password
    public string CookieScope { get; set; } = "strict"; // strict | domain
    public Guid? OidcProviderId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// AllowlistedEmail.cs
public class AllowlistedEmail
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset AddedAt { get; set; }
}

// MagicLink.cs
public class MagicLink
{
    public Guid Id { get; set; }
    public string Token { get; set; } = string.Empty;  // random URL-safe 32+ chars
    public Guid RouteId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}

// IssuedToken.cs — track issued JWTs for revocation
public class IssuedToken
{
    public Guid Id { get; set; }              // = jti
    public string Subject { get; set; } = string.Empty;
    public Guid RouteId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
```

Migration + DbSets.

### Task 2: Ed25519TokenSigner

```csharp
// src/Limen.Infrastructure/Auth/Ed25519TokenSigner.cs
using System.Text.Json;
using NSec.Cryptography;

namespace Limen.Infrastructure.Auth;

public sealed class Ed25519TokenSigner : ITokenSigner
{
    private readonly Key _signKey;  // persisted: read from file, or generated on first startup

    public Ed25519TokenSigner(string keyPath)
    {
        if (File.Exists(keyPath))
        {
            var bytes = File.ReadAllBytes(keyPath);
            _signKey = Key.Import(SignatureAlgorithm.Ed25519, bytes, KeyBlobFormat.RawPrivateKey,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
        }
        else
        {
            _signKey = Key.Create(SignatureAlgorithm.Ed25519,
                new KeyCreationParameters { ExportPolicy = KeyExportPolicies.AllowPlaintextExport });
            File.WriteAllBytes(keyPath, _signKey.Export(KeyBlobFormat.RawPrivateKey));
        }
    }

    public string SignJwt(object headerObj, object payloadObj)
    {
        var header = Base64Url(JsonSerializer.SerializeToUtf8Bytes(headerObj));
        var payload = Base64Url(JsonSerializer.SerializeToUtf8Bytes(payloadObj));
        var signingInput = System.Text.Encoding.UTF8.GetBytes($"{header}.{payload}");
        var sig = SignatureAlgorithm.Ed25519.Sign(_signKey, signingInput);
        return $"{header}.{payload}.{Base64Url(sig)}";
    }

    public byte[] PublicKeyBytes => _signKey.PublicKey.Export(KeyBlobFormat.RawPublicKey);

    private static string Base64Url(byte[] b) =>
        Convert.ToBase64String(b).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
```

Register: `services.AddSingleton<ITokenSigner>(_ => new Ed25519TokenSigner("/data/signing-key.bin"));`

### Task 3: Login endpoints on Limen

`/auth/login?resource={routeId}&return_to={url}` — UI shows form based on route's auth mode.

- **password:** POST `/auth/login-password` { email, password, routeId } → LoginWithPasswordCommand → sets cookie `limen_session` → 302 return_to
- **allowlist:** POST `/auth/magic-request` { email, routeId } → InitiateMagicLinkCommand → emails link → user clicks → GET `/auth/magic/{token}` → VerifyMagicLinkCommand → sets cookie → 302
- **sso:** GET `/auth/resource-oidc?routeId=` → redirects to IdP → callback → sets cookie → 302

Each command writes an `IssuedToken` row for revocation tracking.

### Task 4: JWT structure

Header:
```json
{ "alg": "EdDSA", "typ": "JWT", "kid": "limen-2026-04" }
```
Payload:
```json
{
  "jti": "<guid>",
  "sub": "user@example.com",
  "routeId": "<guid>",
  "authMethod": "allowlist",
  "cookieScope": "strict",
  "iss": "limen",
  "aud": "<routeId>",
  "iat": 1712345678,
  "exp": 1712346578
}
```

TTL: 15 min. UI refreshes silently at 12 min mark via `/auth/refresh`.

### Task 5: Revocation list endpoint

```csharp
app.MapGet("/auth/revoked", async (IMediator m, CancellationToken ct) =>
{
    var list = await m.Send(new GetRevokedTokensQuery(), ct);
    return Results.Ok(list);  // [{ jti, expiresAt }]
});
```

Query filters: revoked AND not-yet-expired (no need to return tokens that are naturally dead).

### Task 6: Public key endpoint

```csharp
app.MapGet("/auth/public-key", (ITokenSigner signer) =>
{
    return Results.Ok(new
    {
        kid = "limen-2026-04",
        alg = "EdDSA",
        publicKey = Convert.ToBase64String(signer.PublicKeyBytes)
    });
});
```

### Task 7: Ostiarius — RevokedTokenPoller + Ed25519Verifier

```csharp
// src/Ostiarius.Infrastructure/Auth/RevokedTokenPoller.cs
public sealed class RevokedTokenPoller : BackgroundService
{
    private readonly HttpClient _http;
    private readonly IRevokedTokenCache _cache;
    // every 30s, GET /auth/revoked from Limen, replace cache
}

// src/Ostiarius.Infrastructure/Auth/Ed25519Verifier.cs
// holds Limen's public key (fetched on boot, refreshed on rotation event)
// verifies Ed25519 signature over header.payload
// returns parsed JwtClaims or throws
```

### Task 8: AuthMiddleware in Ostiarius

Runs BEFORE YARP's routing step:

```csharp
public sealed class AuthMiddleware
{
    private readonly RequestDelegate _next;
    private readonly RouteStore _store;
    private readonly IJwtVerifier _verifier;
    private readonly IRevokedTokenCache _revoked;

    public async Task InvokeAsync(HttpContext ctx)
    {
        var host = ctx.Request.Host.Host;
        var route = _store.Snapshot().FirstOrDefault(r => r.Hostname.Equals(host, StringComparison.OrdinalIgnoreCase));
        if (route is null || route.AuthPolicy == "none") { await _next(ctx); return; }

        var cookie = ctx.Request.Cookies["limen_session"];
        if (string.IsNullOrEmpty(cookie)) { Redirect(ctx, route); return; }

        try
        {
            var claims = _verifier.Verify(cookie);
            if (_revoked.IsRevoked(claims.Jti)) { Redirect(ctx, route); return; }
            if (claims.RouteId != route.RouteId.ToString()) { Redirect(ctx, route); return; }
            if (claims.Exp < DateTimeOffset.UtcNow.ToUnixTimeSeconds()) { Redirect(ctx, route); return; }

            // Inject identity headers
            ctx.Request.Headers["X-Limen-User-Id"] = claims.Subject;
            ctx.Request.Headers["X-Limen-User-Email"] = claims.Subject;
            ctx.Request.Headers["X-Limen-Auth-Method"] = claims.AuthMethod;
            ctx.Request.Headers["X-Limen-Resource-Id"] = claims.RouteId;

            await _next(ctx);
        }
        catch { Redirect(ctx, route); }
    }

    private void Redirect(HttpContext ctx, RouteSpec route)
    {
        var returnTo = Uri.EscapeDataString($"{ctx.Request.Scheme}://{ctx.Request.Host}{ctx.Request.Path}");
        ctx.Response.Redirect($"https://<limen-host>/auth/login?resource={route.RouteId}&return_to={returnTo}");
    }
}
```

Register in `Program.cs` before `MapReverseProxy()`:
```csharp
app.UseMiddleware<AuthMiddleware>();
```

### Task 9: Email sender for magic links

Start simple: SMTP via `MailKit` (or keep it to logging the URL in dev). For v1, optional SMTP config in appsettings; if not configured, admin sees the magic link in logs and can paste it.

### Task 10: UI — auth pages in Limen

- `login.component.ts` — reads `?resource=` query, fetches route's auth mode, renders appropriate form
- Magic verify component shows success after clicking link
- Policy editor on Route detail page (admin can set mode + add allowed emails or password)

### Task 11: E2E smoke test

1. Create a Route `app.example.com` with auth mode `password`, set a password
2. Hit `https://app.example.com` anonymously → redirected to Limen login
3. Enter password → cookie set → redirected back → request proxies through with X-Limen-* headers
4. Revoke via admin UI → within 30s next request 302s back to login
5. Test `allowlist` mode: enter email, check logs for magic link, click, session established

---

## Exit criteria for Plan 6

✅ Ed25519 signing working
✅ AuthMiddleware ahead of YARP
✅ Three auth modes: password, sso, allowlist (magic link)
✅ Identity headers injected on authenticated requests
✅ Revocation list polled every 30s
✅ E2E tests for all three modes

**Plan 7 unlocks next:** polish + E2E + docs site + release pipeline.
