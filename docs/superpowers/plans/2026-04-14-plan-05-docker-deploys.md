# Plan 05 — Docker deploys + auto-update polling

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans`.

**Goal:** Limentinus with `docker` role pulls images and runs containers on command. Limen has a persistent deployment queue. Admin can create/update a Service; a deployment is queued and executed on the target node. Auto-deploy: Quartz job polls the registry; on new digest, a new deployment is queued. Health-check + rollback on failure.

**Architecture:** `DeploymentQueue` table in Postgres (Coolify pattern). Limen orchestrates; Limentinus executes. Deploy is composed of explicit stages (NOT a god class — Coolify anti-pattern).

**Prerequisites:** Plans 1-4 complete.

---

## File structure

**`limen`:**
- `src/Limen.Domain/Deployments/{Deployment.cs, DeploymentStatus.cs, DeploymentStage.cs}`
- `src/Limen.Application/Commands/Deployments/{CreateDeploymentCommand.cs, CancelDeploymentCommand.cs, ReportDeploymentProgressCommand.cs}`
- `src/Limen.Application/Queries/Deployments/{ListDeploymentsQuery.cs, GetDeploymentLogsQuery.cs}`
- `src/Limen.Application/Services/{DeploymentPlanner.cs, RegistryDigestChecker.cs}`
- `src/Limen.Infrastructure/Registry/RegistryClient.cs`
- `src/Limen.Infrastructure/Jobs/RegistryPollJob.cs` (Quartz)
- `contracts/Limen.Contracts/AgentMessages/{DeployCommand.cs, DeployProgress.cs, DeployResult.cs, StopContainerCommand.cs, RollbackCommand.cs}`
- Angular: `features/deployments/*`

**`limentinus`:**
- `src/Limentinus.Domain/Deploy/{DeployContext.cs, DeployStageResult.cs}`
- `src/Limentinus.Application/Services/DeployPipeline.cs`
- `src/Limentinus.Application/Services/Stages/{IDeployStage.cs, PullImageStage.cs, CaptureOldStage.cs, StartNewStage.cs, HealthCheckStage.cs, FinalizeStage.cs, RollbackStage.cs}`
- `src/Limentinus.Infrastructure/Docker/DockerDotNetDriver.cs`

---

## Tasks

### Task 1: Deployment domain entities

```csharp
// src/Limen.Domain/Deployments/DeploymentStatus.cs
public enum DeploymentStatus { Queued, InProgress, Succeeded, Failed, RolledBack, Cancelled }

// src/Limen.Domain/Deployments/Deployment.cs
public class Deployment
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public Guid TargetNodeId { get; set; }
    public string ImageDigest { get; set; } = string.Empty;   // sha256:...
    public string ImageTag { get; set; } = string.Empty;      // :latest
    public DeploymentStatus Status { get; set; }
    public string CurrentStage { get; set; } = string.Empty;
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? EndedAt { get; set; }
    public string Logs { get; set; } = string.Empty;          // appended JSON lines
    public Guid? PreviousDeploymentId { get; set; }           // for rollback context
}
```

Add to DbContext + migration `Deployments`. Unique constraint on `(ServiceId, ImageDigest, Status='Queued'|'InProgress')` via partial index for dedup.

### Task 2: Agent contracts for deploy

```csharp
// contracts/Limen.Contracts/AgentMessages/DeployCommand.cs
public sealed record DeployCommand(
    Guid DeploymentId,
    Guid ServiceId,
    string Image,
    string ContainerName,
    int InternalPort,
    Dictionary<string, string> Env,
    string[] Volumes,
    HealthCheckSpec HealthCheck,
    string NetworkMode);

public sealed record HealthCheckSpec(
    string? Command,   // "curl -f http://localhost:3000/health" OR null for port-only check
    int TimeoutSeconds,
    int MaxRetries,
    int IntervalSeconds);

public sealed record DeployProgress(Guid DeploymentId, string Stage, string Message, int? PercentComplete);
public sealed record DeployResult(Guid DeploymentId, bool Success, string? RolledBackReason);
public sealed record StopContainerCommand(string ContainerName);
public sealed record RollbackCommand(Guid DeploymentId);
```

Add constants to `AgentMessageTypes`:
```csharp
public const string Deploy = "agent/deploy";
public const string DeployProgress = "agent/deployProgress";
public const string DeployResult = "agent/deployResult";
public const string StopContainer = "agent/stopContainer";
public const string Rollback = "agent/rollback";
```

### Task 3: CreateDeploymentCommand with dedup

```csharp
// src/Limen.Application/Commands/Deployments/CreateDeploymentCommand.cs
public sealed record CreateDeploymentCommand(Guid ServiceId, string ImageDigest, string ImageTag) : ICommand<Guid>;

internal sealed class Handler : ICommandHandler<CreateDeploymentCommand, Guid>
{
    // Dedup: if an active deployment exists for (ServiceId, ImageDigest), return existing
    // Else insert Queued row, and if node free, promote to InProgress and send to agent
}
```

### Task 4: RegistryClient — docker registry v2 API

```csharp
// src/Limen.Infrastructure/Registry/RegistryClient.cs
// Pseudocode:
// - Resolve registry from image name ("ghcr.io/foo/bar:latest" → registry=ghcr.io, repo=foo/bar, tag=latest)
// - Auth via Bearer token from registry (or stored credentials)
// - HEAD /v2/<repo>/manifests/<tag> with Accept headers for both OCI and Docker v2 schemas
// - Read `Docker-Content-Digest` header for the manifest digest
```

### Task 5: RegistryPollJob (Quartz)

Per-service cadence (default 5 min, configurable). For each Service with `AutoDeploy=true`:
1. Fetch digest via RegistryClient
2. Compare to last successful deployment's digest
3. If different → send `CreateDeploymentCommand`

Register in Quartz: `AddJob<RegistryPollJob>().WithIdentity("RegistryPoll")` + trigger.

### Task 6: DockerDotNetDriver

```csharp
// src/Limentinus.Infrastructure/Docker/DockerDotNetDriver.cs
using Docker.DotNet;
using Docker.DotNet.Models;

namespace Limentinus.Infrastructure.Docker;

public sealed class DockerDotNetDriver : IDockerDriver
{
    private readonly DockerClient _client;
    public DockerDotNetDriver() =>
        _client = new DockerClientConfiguration(new Uri("unix:///var/run/docker.sock")).CreateClient();

    public async Task<string> PullImageAsync(string image, IProgress<JSONMessage> progress, CancellationToken ct)
    {
        await _client.Images.CreateImageAsync(
            new ImagesCreateParameters { FromImage = image.Split(':')[0], Tag = image.Split(':')[1] },
            null, new Progress<JSONMessage>(m => progress?.Report(m)), ct);
        var inspect = await _client.Images.InspectImageAsync(image, ct);
        return inspect.Id;   // sha256:...
    }

    public async Task<string> StartContainerAsync(CreateContainerParameters p, CancellationToken ct)
    {
        var resp = await _client.Containers.CreateContainerAsync(p, ct);
        await _client.Containers.StartContainerAsync(resp.ID, null, ct);
        return resp.ID;
    }

    public async Task StopContainerAsync(string id, CancellationToken ct)
    {
        await _client.Containers.StopContainerAsync(id, new ContainerStopParameters { WaitBeforeKillSeconds = 30 }, ct);
    }

    public async Task RemoveContainerAsync(string id, CancellationToken ct)
    {
        await _client.Containers.RemoveContainerAsync(id, new ContainerRemoveParameters { Force = true }, ct);
    }

    public async Task<string?> FindContainerIdAsync(string name, CancellationToken ct)
    {
        var list = await _client.Containers.ListContainersAsync(new ContainersListParameters
        { All = true, Filters = new Dictionary<string, IDictionary<string, bool>> { ["name"] = new Dictionary<string, bool> { [name] = true } } }, ct);
        return list.FirstOrDefault()?.ID;
    }
}
```

### Task 7: Deploy stages

```csharp
// src/Limentinus.Application/Services/Stages/IDeployStage.cs
public interface IDeployStage
{
    string Name { get; }
    Task<DeployStageResult> ExecuteAsync(DeployContext ctx, CancellationToken ct);
}

// DeployContext.cs
public sealed class DeployContext
{
    public DeployCommand Request { get; }
    public string? NewContainerId { get; set; }
    public string? OldContainerId { get; set; }
    public IDeployReporter Reporter { get; }
    public DeployContext(DeployCommand r, IDeployReporter rep) { Request = r; Reporter = rep; }
}

public sealed record DeployStageResult(bool Success, string? Error);
```

One file per stage:

- **PullImageStage** — calls `IDockerDriver.PullImageAsync`, reports progress
- **CaptureOldStage** — `driver.FindContainerIdAsync(req.ContainerName)` → stores OldContainerId
- **StartNewStage** — creates container with name `{ContainerName}-{deploymentId[:8]}`
- **HealthCheckStage** — loops N times calling healthcheck (exec or HTTP probe); fails if no success
- **FinalizeStage** — stops old, renames new to `ContainerName`, removes old
- **RollbackStage** — stops new, restarts old (if oldId exists), reports failure

### Task 8: DeployPipeline (NOT a god class — ~50 LoC)

```csharp
// src/Limentinus.Application/Services/DeployPipeline.cs
public sealed class DeployPipeline
{
    private readonly IEnumerable<IDeployStage> _stages;
    private readonly IRollbackStage _rollback;

    public DeployPipeline(IEnumerable<IDeployStage> stages, IRollbackStage rollback)
    { _stages = stages; _rollback = rollback; }

    public async Task<DeployResult> RunAsync(DeployCommand req, IDeployReporter reporter, CancellationToken ct)
    {
        var ctx = new DeployContext(req, reporter);
        foreach (var stage in _stages)
        {
            reporter.ReportProgress(stage.Name, "running");
            var res = await stage.ExecuteAsync(ctx, ct);
            if (!res.Success)
            {
                reporter.ReportProgress(stage.Name, $"failed: {res.Error}");
                await _rollback.ExecuteAsync(ctx, ct);
                return new DeployResult(req.DeploymentId, false, res.Error);
            }
        }
        return new DeployResult(req.DeploymentId, true, null);
    }
}
```

### Task 9: Wire deploy command handling in Limentinus

In `LimenWebSocketChannel.RunAsync`, when receiving `AgentMessageTypes.Deploy`:
1. Deserialize `DeployCommand`
2. Call `DeployPipeline.RunAsync(cmd, reporter, ct)`
3. Send `DeployResult` back via same WS

`IDeployReporter.ReportProgress()` → sends `DeployProgress` messages over WS in real time.

### Task 10: Limen — receive DeployProgress + DeployResult

In `AgentsWebSocketEndpoint`, add handlers for these message types. Persist progress as log lines on `Deployment.Logs` (append `{ stage, message, ts }` JSON per line). Update `Deployment.Status` on `DeployResult`.

### Task 11: UI — Services + Deployments

- **Services page:** form with image, target node, internal port, env vars, volumes, healthcheck, auto-deploy toggle
- **Deployments page:** list all deployments with filter by service; click for details view with live-streaming logs (via WS — subscribe to `/api/deployments/{id}/logs/ws` on backend)
- Health check form: command OR port-only

### Task 12: E2E smoke test

1. Create a Service: `image=nginx:latest`, target=node B, port=80, healthcheck=HTTP GET /, auto-deploy=true
2. Verify deployment runs, container starts, status=Succeeded
3. Verify auto-update: simulate by re-tagging a different nginx image; wait for poll; new deployment queues
4. Verify rollback: introduce a bad image (healthcheck fails); deployment should roll back, old container restored

---

## Exit criteria for Plan 5

✅ DeploymentQueue table + dedup working
✅ DeployPipeline explicit stages; no god class
✅ RegistryPollJob detects new digests
✅ Live deployment logs in UI
✅ Rollback restores previous container on health check failure
✅ E2E: deploy nginx end-to-end

**Plan 6 unlocks next:** resource-level authentication.
