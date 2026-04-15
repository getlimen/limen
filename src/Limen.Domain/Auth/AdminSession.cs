namespace Limen.Domain.Auth;

/// <summary>
/// Persisted admin session. One row per active login.
/// </summary>
public class AdminSession
{
    public Guid Id { get; set; }
    public string Subject { get; set; } = string.Empty; // OIDC subject claim
    public string Email { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
    public string? IpAddress { get; set; }
    public string? UserAgent { get; set; }
}
