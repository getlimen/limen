using System.Text.Json;
using Limen.Application.Common.Interfaces;
using Limen.Domain.Deployments;
using Mediator;

namespace Limen.Application.Commands.Deployments;

public sealed record ReportDeploymentResultCommand(
    Guid DeploymentId,
    bool Success,
    string? RolledBackReason) : ICommand;

internal sealed class ReportDeploymentResultCommandHandler : ICommandHandler<ReportDeploymentResultCommand>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public ReportDeploymentResultCommandHandler(IAppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async ValueTask<Unit> Handle(ReportDeploymentResultCommand cmd, CancellationToken ct)
    {
        var deployment = await _db.Deployments.FindAsync(new object[] { cmd.DeploymentId }, ct)
            ?? throw new InvalidOperationException($"Deployment {cmd.DeploymentId} not found.");

        var now = _clock.UtcNow;
        deployment.EndedAt = now;

        if (cmd.Success)
        {
            deployment.Status = DeploymentStatus.Succeeded;
        }
        else
        {
            deployment.Status = cmd.RolledBackReason is not null
                ? DeploymentStatus.RolledBack
                : DeploymentStatus.Failed;
        }

        var logLine = JsonSerializer.Serialize(new
        {
            ts = now.ToString("O"),
            stage = "result",
            message = cmd.Success ? "Deployment succeeded" : $"Deployment failed: {cmd.RolledBackReason ?? "unknown"}",
            pct = cmd.Success ? 100 : (int?)null,
        }) + "\n";

        deployment.Logs += logLine;

        await _db.SaveChangesAsync(ct);
        return Unit.Value;
    }
}
