namespace Limen.Domain.Nodes;

public class Node
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string[] Roles { get; set; } = Array.Empty<string>();
    public NodeStatus Status { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? LastSeenAt { get; set; }
    public Agent? Agent { get; set; }
}
