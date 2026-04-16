namespace Limen.Domain.Auth;

public class AllowlistedEmail
{
    public Guid Id { get; set; }
    public Guid RouteId { get; set; }
    public string Email { get; set; } = string.Empty;  // lowercased at write-time
    public DateTimeOffset AddedAt { get; set; }
}
