namespace Limen.Domain.Tunnels;

public class WireGuardPeer
{
    public Guid Id { get; set; }
    public Guid AgentId { get; set; }
    public string PublicKey { get; set; } = string.Empty;
    public string TunnelIp { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
