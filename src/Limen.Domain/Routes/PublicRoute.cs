namespace Limen.Domain.Routes;

public class PublicRoute
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public Guid ProxyNodeId { get; set; }
    public string Hostname { get; set; } = string.Empty;
    public bool TlsEnabled { get; set; } = true;
    public string AuthPolicy { get; set; } = "none";
    public DateTimeOffset CreatedAt { get; set; }
}
