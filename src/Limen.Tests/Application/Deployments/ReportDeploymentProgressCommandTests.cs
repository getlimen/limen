using FluentAssertions;
using Limen.Application.Commands.Deployments;
using Limen.Application.Common.Interfaces;
using Limen.Domain.Deployments;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;

namespace Limen.Tests.Application.Deployments;

public sealed class ReportDeploymentProgressCommandTests
{
    private static (AppDbContext db, IClock clock, DateTimeOffset now) MakeContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 04, 15, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);
        return (db, clock, now);
    }

    private static ReportDeploymentProgressCommandHandler MakeHandler(AppDbContext db, IClock clock)
        => new(db, clock);

    private static Deployment SeedDeployment(AppDbContext db, DeploymentStatus status, DateTimeOffset queuedAt)
    {
        var deployment = new Deployment
        {
            Id = Guid.NewGuid(),
            ServiceId = Guid.NewGuid(),
            TargetNodeId = Guid.NewGuid(),
            ImageDigest = "sha256:abc123",
            ImageTag = "latest",
            Status = status,
            QueuedAt = queuedAt,
            Logs = string.Empty,
        };
        db.Deployments.Add(deployment);
        db.SaveChanges();
        return deployment;
    }

    [Fact]
    public async Task Transitions_status_from_Queued_to_InProgress()
    {
        var (db, clock, now) = MakeContext();
        var deployment = SeedDeployment(db, DeploymentStatus.Queued, now.AddMinutes(-1));
        var handler = MakeHandler(db, clock);

        await handler.Handle(
            new ReportDeploymentProgressCommand(deployment.Id, "pulling", "Pulling image...", 10),
            CancellationToken.None);

        var updated = await db.Deployments.FindAsync(new object[] { deployment.Id });
        updated!.Status.Should().Be(DeploymentStatus.InProgress);
        updated.StartedAt.Should().Be(now);
        updated.CurrentStage.Should().Be("pulling");
    }

    [Fact]
    public async Task Does_not_override_StartedAt_if_already_set()
    {
        var (db, clock, now) = MakeContext();
        var deployment = SeedDeployment(db, DeploymentStatus.InProgress, now.AddMinutes(-5));
        var startedAt = now.AddMinutes(-3);
        deployment.StartedAt = startedAt;
        db.SaveChanges();

        var handler = MakeHandler(db, clock);

        await handler.Handle(
            new ReportDeploymentProgressCommand(deployment.Id, "running", "Container started", 50),
            CancellationToken.None);

        var updated = await db.Deployments.FindAsync(new object[] { deployment.Id });
        updated!.StartedAt.Should().Be(startedAt, "StartedAt should not be overwritten");
    }

    [Fact]
    public async Task Appends_log_line_with_json_format()
    {
        var (db, clock, now) = MakeContext();
        var deployment = SeedDeployment(db, DeploymentStatus.Queued, now.AddMinutes(-1));
        var handler = MakeHandler(db, clock);

        await handler.Handle(
            new ReportDeploymentProgressCommand(deployment.Id, "pulling", "Pulling image", 10),
            CancellationToken.None);

        var updated = await db.Deployments.FindAsync(new object[] { deployment.Id });
        updated!.Logs.Should().Contain("\"stage\":\"pulling\"");
        updated.Logs.Should().Contain("\"message\":\"Pulling image\"");
        updated.Logs.Should().Contain("\"pct\":10");
    }

    [Fact]
    public async Task Accumulates_multiple_log_lines()
    {
        var (db, clock, now) = MakeContext();
        var deployment = SeedDeployment(db, DeploymentStatus.Queued, now.AddMinutes(-1));
        var handler = MakeHandler(db, clock);

        await handler.Handle(
            new ReportDeploymentProgressCommand(deployment.Id, "pulling", "Pulling image", 10),
            CancellationToken.None);

        await handler.Handle(
            new ReportDeploymentProgressCommand(deployment.Id, "starting", "Starting container", 80),
            CancellationToken.None);

        var updated = await db.Deployments.FindAsync(new object[] { deployment.Id });
        var lines = updated!.Logs.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(2);
    }

    [Fact]
    public async Task Throws_when_deployment_not_found()
    {
        var (db, clock, _) = MakeContext();
        var handler = MakeHandler(db, clock);

        var act = async () => await handler.Handle(
            new ReportDeploymentProgressCommand(Guid.NewGuid(), "pulling", "Pulling", null),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }
}
