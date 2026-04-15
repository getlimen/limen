namespace Limen.Domain.Services;

public class Service
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TargetNodeId { get; set; }
    public string ContainerName { get; set; } = string.Empty;
    public int InternalPort { get; set; }
    public string Image { get; set; } = string.Empty;
    public bool AutoDeploy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
