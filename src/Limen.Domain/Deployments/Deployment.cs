namespace Limen.Domain.Deployments;

public class Deployment
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public Guid TargetNodeId { get; set; }
    public string ImageDigest { get; set; } = string.Empty;
    public string ImageTag { get; set; } = string.Empty;
    public DeploymentStatus Status { get; set; }
    public string CurrentStage { get; set; } = string.Empty;
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string Logs { get; set; } = string.Empty;
    public Guid? PreviousDeploymentId { get; set; }
}
