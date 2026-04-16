namespace Limen.Domain.Auth;

public class IssuedToken
{
    public Guid Id { get; set; }   // = jti claim
    public string Subject { get; set; } = string.Empty;
    public Guid RouteId { get; set; }
    public DateTimeOffset IssuedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
