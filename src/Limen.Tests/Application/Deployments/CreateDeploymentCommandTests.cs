using FluentAssertions;
using Limen.Application.Commands.Deployments;
using Limen.Application.Common.Interfaces;
using Limen.Domain.Auth;
using Limen.Domain.Deployments;
using Limen.Domain.Nodes;
using Limen.Domain.Routes;
using Limen.Domain.Services;
using Limen.Domain.Tunnels;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;

namespace Limen.Tests.Application.Deployments;

public sealed class CreateDeploymentCommandTests
{
    private static (AppDbContext db, IClock clock, IDeploymentDispatcher dispatcher, DateTimeOffset now) MakeContext()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 04, 15, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);
        var dispatcher = Substitute.For<IDeploymentDispatcher>();
        dispatcher.DispatchAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns(true);
        return (db, clock, dispatcher, now);
    }

    private static CreateDeploymentCommandHandler MakeHandler(AppDbContext db, IClock clock, IDeploymentDispatcher dispatcher)
        => new(db, clock, dispatcher, NullLogger<CreateDeploymentCommandHandler>.Instance);

    private static Service SeedService(AppDbContext db, Guid? nodeId = null)
    {
        var service = new Service
        {
            Id = Guid.NewGuid(),
            Name = "test-svc",
            TargetNodeId = nodeId ?? Guid.NewGuid(),
            ContainerName = "test-container",
            InternalPort = 8080,
            Image = "nginx:latest",
            AutoDeploy = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        db.Services.Add(service);
        db.SaveChanges();
        return service;
    }

    [Fact]
    public async Task Creates_new_deployment_and_returns_id()
    {
        var (db, clock, dispatcher, _) = MakeContext();
        var service = SeedService(db);
        var handler = MakeHandler(db, clock, dispatcher);

        var id = await handler.Handle(
            new CreateDeploymentCommand(service.Id, "sha256:abc123", "latest"),
            CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        (await db.Deployments.CountAsync()).Should().Be(1);
        var dep = await db.Deployments.FirstAsync();
        dep.Status.Should().Be(DeploymentStatus.Queued);
        dep.ServiceId.Should().Be(service.Id);
        dep.ImageDigest.Should().Be("sha256:abc123");
    }

    [Fact]
    public async Task Returns_existing_id_when_queued_deployment_exists_for_same_digest()
    {
        var (db, clock, dispatcher, now) = MakeContext();
        var service = SeedService(db);

        var existingDeployment = new Deployment
        {
            Id = Guid.NewGuid(),
            ServiceId = service.Id,
            TargetNodeId = service.TargetNodeId,
            ImageDigest = "sha256:abc123",
            ImageTag = "latest",
            Status = DeploymentStatus.Queued,
            QueuedAt = now,
        };
        db.Deployments.Add(existingDeployment);
        db.SaveChanges();

        var handler = MakeHandler(db, clock, dispatcher);

        var returnedId = await handler.Handle(
            new CreateDeploymentCommand(service.Id, "sha256:abc123", "latest"),
            CancellationToken.None);

        returnedId.Should().Be(existingDeployment.Id);
        (await db.Deployments.CountAsync()).Should().Be(1, "no second row should be created");
    }

    [Fact]
    public async Task Returns_existing_id_when_inprogress_deployment_exists_for_same_digest()
    {
        var (db, clock, dispatcher, now) = MakeContext();
        var service = SeedService(db);

        var existingDeployment = new Deployment
        {
            Id = Guid.NewGuid(),
            ServiceId = service.Id,
            TargetNodeId = service.TargetNodeId,
            ImageDigest = "sha256:abc123",
            ImageTag = "latest",
            Status = DeploymentStatus.InProgress,
            QueuedAt = now.AddMinutes(-2),
            StartedAt = now.AddMinutes(-1),
        };
        db.Deployments.Add(existingDeployment);
        db.SaveChanges();

        var handler = MakeHandler(db, clock, dispatcher);

        var returnedId = await handler.Handle(
            new CreateDeploymentCommand(service.Id, "sha256:abc123", "latest"),
            CancellationToken.None);

        returnedId.Should().Be(existingDeployment.Id);
        (await db.Deployments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Creates_new_deployment_when_previous_succeeded()
    {
        var (db, clock, dispatcher, now) = MakeContext();
        var service = SeedService(db);

        var previousDeployment = new Deployment
        {
            Id = Guid.NewGuid(),
            ServiceId = service.Id,
            TargetNodeId = service.TargetNodeId,
            ImageDigest = "sha256:abc123",
            ImageTag = "latest",
            Status = DeploymentStatus.Succeeded,
            QueuedAt = now.AddMinutes(-10),
            EndedAt = now.AddMinutes(-5),
        };
        db.Deployments.Add(previousDeployment);
        db.SaveChanges();

        var handler = MakeHandler(db, clock, dispatcher);

        var newId = await handler.Handle(
            new CreateDeploymentCommand(service.Id, "sha256:abc123", "latest"),
            CancellationToken.None);

        newId.Should().NotBe(previousDeployment.Id);
        (await db.Deployments.CountAsync()).Should().Be(2);
    }

    [Fact]
    public async Task Throws_when_service_not_found()
    {
        var (db, clock, dispatcher, _) = MakeContext();
        var handler = MakeHandler(db, clock, dispatcher);

        var act = async () => await handler.Handle(
            new CreateDeploymentCommand(Guid.NewGuid(), "sha256:abc123", "latest"),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*not found*");
    }

    [Fact]
    public async Task Dispatch_failure_is_swallowed_and_deployment_is_still_created()
    {
        var (db, clock, dispatcher, _) = MakeContext();
        var service = SeedService(db);
        dispatcher.DispatchAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new Exception("dispatch unavailable"));
        var handler = MakeHandler(db, clock, dispatcher);

        var id = await handler.Handle(
            new CreateDeploymentCommand(service.Id, "sha256:abc123", "latest"),
            CancellationToken.None);

        id.Should().NotBe(Guid.Empty);
        (await db.Deployments.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task Race_DbUpdateException_UniqueViolation_ReturnsWinner()
    {
        // Arrange: use a wrapper IAppDbContext that throws a unique-violation DbUpdateException
        // on the deployment INSERT, simulating a concurrent INSERT winning the race.
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var realDb = new AppDbContext(opts);

        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 04, 15, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);
        var dispatcher = Substitute.For<IDeploymentDispatcher>();

        var service = new Service
        {
            Id = Guid.NewGuid(),
            Name = "race-svc",
            TargetNodeId = Guid.NewGuid(),
            ContainerName = "race-container",
            InternalPort = 8080,
            Image = "nginx:latest",
            AutoDeploy = true,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        realDb.Services.Add(service);
        realDb.SaveChanges();

        // Seed the "winner" deployment that the concurrent insert already created
        var winner = new Deployment
        {
            Id = Guid.NewGuid(),
            ServiceId = service.Id,
            TargetNodeId = service.TargetNodeId,
            ImageDigest = "sha256:race",
            ImageTag = "latest",
            Status = DeploymentStatus.Queued,
            QueuedAt = now,
        };
        realDb.Deployments.Add(winner);
        realDb.SaveChanges();

        // Wrap the real context so SaveChangesAsync throws unique-violation once
        var wrappingDb = new UniqueViolationOnSaveDbContext(realDb);

        var handler = new CreateDeploymentCommandHandler(wrappingDb, clock, dispatcher, NullLogger<CreateDeploymentCommandHandler>.Instance);

        // Act
        var resultId = await handler.Handle(
            new CreateDeploymentCommand(service.Id, "sha256:race", "latest"),
            CancellationToken.None);

        // Assert: handler must return the winner's id without rethrowing
        resultId.Should().Be(winner.Id);
    }

    /// <summary>
    /// Wraps a real AppDbContext, forwarding all DbSet access but throwing a
    /// unique-violation DbUpdateException on the first SaveChangesAsync call.
    /// </summary>
    private sealed class UniqueViolationOnSaveDbContext(AppDbContext inner) : IAppDbContext
    {
        private bool _shouldThrow = true;

        public DbSet<AdminSession> AdminSessions => inner.AdminSessions;
        public DbSet<Node> Nodes => inner.Nodes;
        public DbSet<Agent> Agents => inner.Agents;
        public DbSet<ProvisioningKey> ProvisioningKeys => inner.ProvisioningKeys;
        public DbSet<WireGuardPeer> WireGuardPeers => inner.WireGuardPeers;
        public DbSet<Service> Services => inner.Services;
        public DbSet<PublicRoute> PublicRoutes => inner.PublicRoutes;
        public DbSet<Deployment> Deployments => inner.Deployments;

        public Task<int> SaveChangesAsync(CancellationToken ct)
        {
            if (_shouldThrow)
            {
                _shouldThrow = false;
                throw new DbUpdateException("simulated unique violation",
                    new FakeSqlStateException("23505"));
            }
            return inner.SaveChangesAsync(ct);
        }
    }

    private sealed class FakeSqlStateException(string sqlState) : Exception("simulated pg exception")
    {
        // The handler detects unique violations by reading this property via reflection.
        public string SqlState { get; } = sqlState;
    }
}
