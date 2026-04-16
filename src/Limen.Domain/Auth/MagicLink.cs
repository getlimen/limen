namespace Limen.Domain.Auth;

public class MagicLink
{
    public Guid Id { get; set; }
    public string TokenHash { get; set; } = string.Empty; // SHA-256 of token; store hash not plaintext
    public Guid RouteId { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
}
