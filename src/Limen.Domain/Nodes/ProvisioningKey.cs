namespace Limen.Domain.Nodes;

public class ProvisioningKey
{
    public Guid Id { get; set; }
    public string KeyHash { get; set; } = string.Empty;
    public string[] IntendedRoles { get; set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public Guid? ResultingNodeId { get; set; }
}
