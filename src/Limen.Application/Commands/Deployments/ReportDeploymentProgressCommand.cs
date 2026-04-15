using System.Text.Json;
using Limen.Application.Common.Interfaces;
using Limen.Domain.Deployments;
using Mediator;

namespace Limen.Application.Commands.Deployments;

public sealed record ReportDeploymentProgressCommand(
    Guid DeploymentId,
    string Stage,
    string Message,
    int? PercentComplete) : ICommand;

internal sealed class ReportDeploymentProgressCommandHandler : ICommandHandler<ReportDeploymentProgressCommand>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public ReportDeploymentProgressCommandHandler(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async ValueTask<Unit> Handle(ReportDeploymentProgressCommand cmd, CancellationToken ct)
    {
        var deployment = await _db.Deployments.FindAsync(new object[] { cmd.DeploymentId }, ct)
            ?? throw new InvalidOperationException($"Deployment {cmd.DeploymentId} not found.");

        var now = _clock.UtcNow;

        if (deployment.Status == DeploymentStatus.Queued)
        {
            deployment.Status = DeploymentStatus.InProgress;
            deployment.StartedAt ??= now;
        }

        deployment.CurrentStage = cmd.Stage;

        var logLine = JsonSerializer.Serialize(new
        {
            ts = now.ToString("O"),
            stage = cmd.Stage,
            message = cmd.Message,
            pct = cmd.PercentComplete,
        }) + "\n";

        deployment.Logs += logLine;

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
