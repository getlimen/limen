namespace Limen.Domain.Auth;

public class ResourceAuthPolicy
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public string Mode { get; set; } = "none"; // none | password | sso | allowlist
    public string? PasswordHash { get; set; }  // argon2id encoded
    public string CookieScope { get; set; } = "strict"; // strict | domain
    public Guid? OidcProviderId { get; set; }  // unused for v1 (single global OIDC); reserved
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
