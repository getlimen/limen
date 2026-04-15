namespace Limen.Domain.Nodes;

public class Agent
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }
    public byte[] SecretHash { get; set; } = Array.Empty<byte>();
    public string AgentVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public DateTimeOffset EnrolledAt { get; set; }
}
