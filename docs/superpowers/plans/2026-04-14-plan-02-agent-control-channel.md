# Plan 02 — Agent control channel: `limentinus` enrollment + WebSocket

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans`.

**Goal:** Scaffold the `limentinus` agent repo. Add WebSocket server in Limen + WS client in Limentinus. Admin can issue a single-use provisioning key, Limentinus enrolls, and the UI shows the node as active with heartbeat. No WG, no Docker role yet — control plane only.

**Architecture:** Agent-initiated JSON-over-WebSocket (matches Newt/Komodo/Portainer). Two-tier credentials: provisioning key (single-use, TTL 15 min) → permanent `agentId` + `secret`. Auto-reconnect with exponential backoff. `ConfigVersion` for dedup.

**Tech Stack:** .NET 10 NativeAOT (for Limentinus), `System.Net.WebSockets`, `Microsoft.AspNetCore.WebSockets`, Mediator, Serilog.

**Prerequisites:** Plan 1 complete. `getlimen/limen` has working scaffold. New repo `getlimen/limentinus` created, empty.

---

## File structure created by this plan

**In `limen` repo (manager side):**
- `contracts/Limen.Contracts/AgentMessages/` — DTOs for agent traffic
- `src/Limen.Domain/Nodes/{Node.cs, Agent.cs, ProvisioningKey.cs}`
- `src/Limen.Application/Commands/Nodes/{CreateProvisioningKeyCommand.cs, EnrollAgentCommand.cs, MarkAgentOfflineCommand.cs}`
- `src/Limen.Application/Queries/Nodes/{ListNodesQuery.cs, GetNodeQuery.cs}`
- `src/Limen.Application/Services/AgentConnectionRegistry.cs`
- `src/Limen.Infrastructure/Agents/{AgentWebSocketHandler.cs, AgentConnectionRegistry.cs}`
- `src/Limen.API/Endpoints/{NodesEndpoints.cs, AgentsWebSocketEndpoint.cs}`
- `src/Limen.Frontend/src/app/features/nodes/{nodes.component.ts, node-detail.component.ts}`

**In `limentinus` repo (new):**
- Full clean-arch scaffold mirroring Plan 1 pattern
- `src/Limentinus.Domain/Node/{NodeIdentity.cs, RoleSet.cs}`
- `src/Limentinus.Application/Services/{EnrollmentService.cs, HeartbeatService.cs}`
- `src/Limentinus.Infrastructure/Control/LimenWebSocketChannel.cs`
- `src/Limentinus.API/Program.cs` (worker service host)
- `compose.yml`
- `Dockerfile`

---

## Tasks

### Task 1: Agent message contracts in Limen.Contracts

**Files:**
- Create: `limen/contracts/Limen.Contracts/AgentMessages/EnrollRequest.cs`
- Create: `limen/contracts/Limen.Contracts/AgentMessages/EnrollResponse.cs`
- Create: `limen/contracts/Limen.Contracts/AgentMessages/Heartbeat.cs`
- Create: `limen/contracts/Limen.Contracts/AgentMessages/AgentMessageTypes.cs`

- [ ] **Step 1: Create `AgentMessageTypes.cs` — string constants for Envelope.Type**

```csharp
// contracts/Limen.Contracts/AgentMessages/AgentMessageTypes.cs
namespace Limen.Contracts.AgentMessages;

public static class AgentMessageTypes
{
    public const string Enroll = "agent/enroll";
    public const string EnrollResponse = "agent/enrollResponse";
    public const string Heartbeat = "agent/heartbeat";
    public const string HeartbeatAck = "agent/heartbeatAck";
    public const string Disconnecting = "agent/disconnecting";
}
```

- [ ] **Step 2: Create `EnrollRequest` and `EnrollResponse`**

```csharp
// contracts/Limen.Contracts/AgentMessages/EnrollRequest.cs
namespace Limen.Contracts.AgentMessages;

public sealed record EnrollRequest(
    string ProvisioningKey,
    string Hostname,
    string[] Roles,
    string Platform,
    string AgentVersion);

public sealed record EnrollResponse(
    Guid AgentId,
    string PermanentSecret,
    string TunnelSubnet);    // reserved for Plan 3; empty string in Plan 2
```

- [ ] **Step 3: Create `Heartbeat`**

```csharp
// contracts/Limen.Contracts/AgentMessages/Heartbeat.cs
namespace Limen.Contracts.AgentMessages;

public sealed record Heartbeat(DateTimeOffset Timestamp, string[] ActiveRoles);
public sealed record HeartbeatAck(ulong ServerVersion);
```

- [ ] **Step 4: Build + commit**

```bash
cd limen
dotnet build contracts/Limen.Contracts/Limen.Contracts.csproj
git add contracts/
git commit -m "feat(contracts): agent message types (EnrollRequest/Response, Heartbeat)"
```

---

### Task 2: Domain entities — Node, Agent, ProvisioningKey

**Files:**
- Create: `limen/src/Limen.Domain/Nodes/Node.cs`
- Create: `limen/src/Limen.Domain/Nodes/Agent.cs`
- Create: `limen/src/Limen.Domain/Nodes/ProvisioningKey.cs`
- Create: `limen/src/Limen.Domain/Nodes/NodeStatus.cs`
- Create: `limen/src/Limen.Infrastructure/Persistence/Configurations/{NodeConfiguration.cs, AgentConfiguration.cs, ProvisioningKeyConfiguration.cs}`
- Modify: `limen/src/Limen.Infrastructure/Persistence/AppDbContext.cs`

- [ ] **Step 1: Create `NodeStatus`**

```csharp
// src/Limen.Domain/Nodes/NodeStatus.cs
namespace Limen.Domain.Nodes;

public enum NodeStatus { Pending, Active, Disconnected, Offline }
```

Note: the user's rule says Domain should only contain DB entity models. Enums used as column types are persisted with entities, so they're allowed here — document this in comments if reviewers ask.

- [ ] **Step 2: Create `Node` and `Agent`**

```csharp
// src/Limen.Domain/Nodes/Node.cs
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
```

```csharp
// src/Limen.Domain/Nodes/Agent.cs
namespace Limen.Domain.Nodes;

public class Agent
{
    public Guid Id { get; set; }
    public Guid NodeId { get; set; }
    public byte[] SecretHash { get; set; } = Array.Empty<byte>(); // bcrypt/argon2 hash of permanent secret
    public string AgentVersion { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string Hostname { get; set; } = string.Empty;
    public DateTimeOffset EnrolledAt { get; set; }
}
```

- [ ] **Step 3: Create `ProvisioningKey`**

```csharp
// src/Limen.Domain/Nodes/ProvisioningKey.cs
namespace Limen.Domain.Nodes;

public class ProvisioningKey
{
    public Guid Id { get; set; }
    public string KeyHash { get; set; } = string.Empty;   // SHA-256 hex of the plaintext key
    public string[] IntendedRoles { get; set; } = Array.Empty<string>();
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? UsedAt { get; set; }
    public Guid? ResultingNodeId { get; set; }
}
```

- [ ] **Step 4: Add EF configurations**

```csharp
// src/Limen.Infrastructure/Persistence/Configurations/NodeConfiguration.cs
using Limen.Domain.Nodes;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Limen.Infrastructure.Persistence.Configurations;

public sealed class NodeConfiguration : IEntityTypeConfiguration<Node>
{
    public void Configure(EntityTypeBuilder<Node> b)
    {
        b.ToTable("nodes");
        b.HasKey(n => n.Id);
        b.Property(n => n.Name).IsRequired().HasMaxLength(128);
        b.Property(n => n.Roles).HasColumnType("text[]");
        b.Property(n => n.Status).HasConversion<string>().HasMaxLength(32);
        b.HasOne(n => n.Agent).WithOne().HasForeignKey<Agent>(a => a.NodeId).OnDelete(DeleteBehavior.Cascade);
        b.HasIndex(n => n.Status);
    }
}
```

```csharp
// src/Limen.Infrastructure/Persistence/Configurations/AgentConfiguration.cs
public sealed class AgentConfiguration : IEntityTypeConfiguration<Agent>
{
    public void Configure(EntityTypeBuilder<Agent> b)
    {
        b.ToTable("agents");
        b.HasKey(a => a.Id);
        b.Property(a => a.SecretHash).IsRequired();
        b.Property(a => a.Hostname).HasMaxLength(256);
        b.Property(a => a.Platform).HasMaxLength(64);
        b.Property(a => a.AgentVersion).HasMaxLength(32);
    }
}
```

```csharp
// src/Limen.Infrastructure/Persistence/Configurations/ProvisioningKeyConfiguration.cs
public sealed class ProvisioningKeyConfiguration : IEntityTypeConfiguration<ProvisioningKey>
{
    public void Configure(EntityTypeBuilder<ProvisioningKey> b)
    {
        b.ToTable("provisioning_keys");
        b.HasKey(x => x.Id);
        b.Property(x => x.KeyHash).IsRequired().HasMaxLength(128);
        b.Property(x => x.IntendedRoles).HasColumnType("text[]");
        b.HasIndex(x => x.KeyHash).IsUnique();
        b.HasIndex(x => x.ExpiresAt);
    }
}
```

- [ ] **Step 5: Update `AppDbContext` + `IAppDbContext`**

Add `DbSet<Node> Nodes`, `DbSet<Agent> Agents`, `DbSet<ProvisioningKey> ProvisioningKeys` to both.

- [ ] **Step 6: Generate migration**

```bash
dotnet ef migrations add Nodes --project src/Limen.Infrastructure --startup-project src/Limen.API
```

- [ ] **Step 7: Build + commit**

```bash
dotnet build
git add src/
git commit -m "feat(domain): Node, Agent, ProvisioningKey entities + migration"
```

---

### Task 3: CreateProvisioningKeyCommand (TDD)

**Files:**
- Create: `limen/src/Limen.Application/Commands/Nodes/CreateProvisioningKeyCommand.cs`
- Create: `limen/src/Limen.Tests/Application/Nodes/CreateProvisioningKeyCommandTests.cs`

- [ ] **Step 1: Write failing test**

```csharp
// src/Limen.Tests/Application/Nodes/CreateProvisioningKeyCommandTests.cs
using FluentAssertions;
using Limen.Application.Commands.Nodes;
using Limen.Application.Common.Interfaces;
using Limen.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Xunit;

namespace Limen.Tests.Application.Nodes;

public sealed class CreateProvisioningKeyCommandTests
{
    [Fact]
    public async Task Creates_one_shot_key_with_15min_TTL()
    {
        var opts = new DbContextOptionsBuilder<AppDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(opts);
        var clock = Substitute.For<IClock>();
        var now = new DateTimeOffset(2026, 04, 14, 12, 0, 0, TimeSpan.Zero);
        clock.UtcNow.Returns(now);

        var handler = new CreateProvisioningKeyCommandHandler(db, clock);
        var result = await handler.Handle(new CreateProvisioningKeyCommand(new[] { "docker" }), CancellationToken.None);

        result.PlaintextKey.Should().NotBeNullOrWhiteSpace();
        result.PlaintextKey.Length.Should().BeGreaterThan(32); // at least 32 chars for entropy
        var stored = await db.ProvisioningKeys.FirstAsync();
        stored.ExpiresAt.Should().Be(now.AddMinutes(15));
        stored.IntendedRoles.Should().Contain("docker");
        stored.UsedAt.Should().BeNull();
    }
}
```

- [ ] **Step 2: Run (expect FAIL)**

- [ ] **Step 3: Write command + handler**

```csharp
// src/Limen.Application/Commands/Nodes/CreateProvisioningKeyCommand.cs
using System.Security.Cryptography;
using System.Text;
using Limen.Application.Common.Interfaces;
using Limen.Domain.Nodes;
using Mediator;

namespace Limen.Application.Commands.Nodes;

public sealed record CreateProvisioningKeyResult(Guid Id, string PlaintextKey, DateTimeOffset ExpiresAt);
public sealed record CreateProvisioningKeyCommand(string[] IntendedRoles) : ICommand<CreateProvisioningKeyResult>;

internal sealed class CreateProvisioningKeyCommandHandler : ICommandHandler<CreateProvisioningKeyCommand, CreateProvisioningKeyResult>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public CreateProvisioningKeyCommandHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async ValueTask<CreateProvisioningKeyResult> Handle(CreateProvisioningKeyCommand cmd, CancellationToken ct)
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        var plaintext = Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_'); // URL-safe
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(plaintext)));
        var now = _clock.UtcNow;
        var pk = new ProvisioningKey
        {
            Id = Guid.NewGuid(),
            KeyHash = hash,
            IntendedRoles = cmd.IntendedRoles,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15),
        };
        _db.ProvisioningKeys.Add(pk);
        await _db.SaveChangesAsync(ct);
        return new CreateProvisioningKeyResult(pk.Id, plaintext, pk.ExpiresAt);
    }
}
```

- [ ] **Step 4: Run (expect PASS); commit**

```bash
dotnet test --filter "CreateProvisioningKeyCommandTests"
git add src/
git commit -m "feat(app): CreateProvisioningKeyCommand (one-shot, 15 min TTL)"
```

---

### Task 4: EnrollAgentCommand (TDD)

**Files:**
- Create: `limen/src/Limen.Application/Commands/Nodes/EnrollAgentCommand.cs`
- Create: `limen/src/Limen.Tests/Application/Nodes/EnrollAgentCommandTests.cs`

- [ ] **Step 1: Failing test**

```csharp
[Fact]
public async Task Enrolling_with_valid_key_creates_Node_and_Agent_and_burns_key()
{
    // arrange: seed a valid provisioning key
    // act: run EnrollAgentCommand with matching plaintext
    // assert: Node + Agent persisted; key.UsedAt != null; response contains agentId + secret
}

[Fact]
public async Task Enrolling_with_expired_key_throws()
{
    // ...
}

[Fact]
public async Task Enrolling_with_already_used_key_throws()
{
    // ...
}
```

(Write full test code using same scaffolding pattern as Task 3.)

- [ ] **Step 2: Run (expect FAIL)**

- [ ] **Step 3: Implement command + handler**

```csharp
// src/Limen.Application/Commands/Nodes/EnrollAgentCommand.cs
using System.Security.Cryptography;
using System.Text;
using Limen.Application.Common.Interfaces;
using Limen.Domain.Nodes;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Commands.Nodes;

public sealed record EnrollAgentResult(Guid AgentId, string Secret);
public sealed record EnrollAgentCommand(
    string ProvisioningKeyPlaintext,
    string Hostname,
    string[] Roles,
    string Platform,
    string AgentVersion) : ICommand<EnrollAgentResult>;

internal sealed class EnrollAgentCommandHandler : ICommandHandler<EnrollAgentCommand, EnrollAgentResult>
{
    private readonly IAppDbContext _db;
    private readonly IClock _clock;

    public EnrollAgentCommandHandler(IAppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async ValueTask<EnrollAgentResult> Handle(EnrollAgentCommand cmd, CancellationToken ct)
    {
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(cmd.ProvisioningKeyPlaintext)));
        var now = _clock.UtcNow;
        var pk = await _db.ProvisioningKeys.FirstOrDefaultAsync(x => x.KeyHash == hash, ct)
            ?? throw new InvalidOperationException("Invalid provisioning key.");
        if (pk.UsedAt is not null) throw new InvalidOperationException("Provisioning key already used.");
        if (pk.ExpiresAt < now) throw new InvalidOperationException("Provisioning key expired.");

        var node = new Node
        {
            Id = Guid.NewGuid(),
            Name = cmd.Hostname,
            Roles = cmd.Roles,
            Status = NodeStatus.Pending,
            CreatedAt = now,
        };
        var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=')
            .Replace('+', '-').Replace('/', '_');
        var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
        var agent = new Agent
        {
            Id = Guid.NewGuid(),
            NodeId = node.Id,
            SecretHash = secretHash,
            AgentVersion = cmd.AgentVersion,
            Platform = cmd.Platform,
            Hostname = cmd.Hostname,
            EnrolledAt = now,
        };
        _db.Nodes.Add(node);
        _db.Agents.Add(agent);
        pk.UsedAt = now;
        pk.ResultingNodeId = node.Id;
        await _db.SaveChangesAsync(ct);
        return new EnrollAgentResult(agent.Id, secret);
    }
}
```

- [ ] **Step 4: Run + commit**

```bash
dotnet test --filter "EnrollAgentCommandTests"
git add src/
git commit -m "feat(app): EnrollAgentCommand — validates provisioning key, creates Node+Agent, burns key"
```

---

### Task 5: AgentConnectionRegistry (tracking live WS connections)

**Files:**
- Create: `limen/src/Limen.Application/Common/Interfaces/IAgentConnectionRegistry.cs`
- Create: `limen/src/Limen.Infrastructure/Agents/AgentConnectionRegistry.cs`
- Modify: `limen/src/Limen.Infrastructure/DependencyInjection.cs`

- [ ] **Step 1: Define interface**

```csharp
// src/Limen.Application/Common/Interfaces/IAgentConnectionRegistry.cs
namespace Limen.Application.Common.Interfaces;

public interface IAgentConnectionRegistry
{
    Task RegisterAsync(Guid agentId, IAgentChannel channel);
    void Unregister(Guid agentId);
    IAgentChannel? Get(Guid agentId);
    IReadOnlyCollection<Guid> ListOnlineAgentIds();
}

public interface IAgentChannel
{
    Task SendJsonAsync<T>(string type, T payload, CancellationToken ct);
    Task CloseAsync();
}
```

- [ ] **Step 2: Implement in Infrastructure**

```csharp
// src/Limen.Infrastructure/Agents/AgentConnectionRegistry.cs
using System.Collections.Concurrent;
using Limen.Application.Common.Interfaces;

namespace Limen.Infrastructure.Agents;

public sealed class AgentConnectionRegistry : IAgentConnectionRegistry
{
    private readonly ConcurrentDictionary<Guid, IAgentChannel> _channels = new();

    public Task RegisterAsync(Guid agentId, IAgentChannel channel)
    {
        _channels.AddOrUpdate(agentId, channel, (_, old) => { _ = old.CloseAsync(); return channel; });
        return Task.CompletedTask;
    }

    public void Unregister(Guid agentId) => _channels.TryRemove(agentId, out _);
    public IAgentChannel? Get(Guid agentId) => _channels.TryGetValue(agentId, out var c) ? c : null;
    public IReadOnlyCollection<Guid> ListOnlineAgentIds() => _channels.Keys.ToArray();
}
```

Register as singleton in `Limen.Infrastructure/DependencyInjection.cs`:
```csharp
services.AddSingleton<IAgentConnectionRegistry, AgentConnectionRegistry>();
```

- [ ] **Step 3: Commit**

```bash
git add src/
git commit -m "feat(app/infra): IAgentConnectionRegistry + in-memory registry"
```

---

### Task 6: WebSocket endpoint + AgentChannel

**Files:**
- Create: `limen/src/Limen.Infrastructure/Agents/AgentWebSocketChannel.cs`
- Create: `limen/src/Limen.API/Endpoints/AgentsWebSocketEndpoint.cs`
- Modify: `limen/src/Limen.API/Program.cs`

- [ ] **Step 1: Implement `AgentWebSocketChannel`**

```csharp
// src/Limen.Infrastructure/Agents/AgentWebSocketChannel.cs
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Limen.Application.Common.Interfaces;
using Limen.Contracts.Common;

namespace Limen.Infrastructure.Agents;

public sealed class AgentWebSocketChannel : IAgentChannel
{
    private readonly WebSocket _ws;
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    public AgentWebSocketChannel(WebSocket ws) => _ws = ws;

    public async Task SendJsonAsync<T>(string type, T payload, CancellationToken ct)
    {
        var env = new Envelope<T>(type, ConfigVersion.Zero, payload);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(env);
        await _sendLock.WaitAsync(ct);
        try { await _ws.SendAsync(bytes, WebSocketMessageType.Text, true, ct); }
        finally { _sendLock.Release(); }
    }

    public async Task CloseAsync()
    {
        if (_ws.State == WebSocketState.Open)
            await _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "server-close", CancellationToken.None);
    }
}
```

- [ ] **Step 2: Implement `AgentsWebSocketEndpoint`**

```csharp
// src/Limen.API/Endpoints/AgentsWebSocketEndpoint.cs
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Limen.Application.Commands.Nodes;
using Limen.Application.Common.Interfaces;
using Limen.Contracts.AgentMessages;
using Limen.Contracts.Common;
using Limen.Infrastructure.Agents;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.API.Endpoints;

public static class AgentsWebSocketEndpoint
{
    public static IEndpointRouteBuilder MapAgentsWebSocket(this IEndpointRouteBuilder app)
    {
        app.Map("/api/agents/ws", async (HttpContext ctx, IMediator mediator,
            IAppDbContext db, IAgentConnectionRegistry registry, ILogger<object> logger) =>
        {
            if (!ctx.WebSockets.IsWebSocketRequest) return Results.BadRequest("Expected WebSocket");

            using var ws = await ctx.WebSockets.AcceptWebSocketAsync();
            var channel = new AgentWebSocketChannel(ws);

            // Phase 1: expect EnrollRequest or Heartbeat (with creds)
            var buf = new byte[16 * 1024];
            var rec = await ws.ReceiveAsync(buf, ctx.RequestAborted);
            if (rec.MessageType != WebSocketMessageType.Text) { await ws.CloseAsync(WebSocketCloseStatus.InvalidPayloadData, "expected text", default); return Results.Empty; }

            var envJson = Encoding.UTF8.GetString(buf, 0, rec.Count);
            using var doc = JsonDocument.Parse(envJson);
            var type = doc.RootElement.GetProperty("Type").GetString();
            Guid agentId;

            if (type == AgentMessageTypes.Enroll)
            {
                var payload = doc.RootElement.GetProperty("Payload").Deserialize<EnrollRequest>()!;
                var result = await mediator.Send(new EnrollAgentCommand(
                    payload.ProvisioningKey, payload.Hostname, payload.Roles, payload.Platform, payload.AgentVersion));
                agentId = result.AgentId;
                await channel.SendJsonAsync(AgentMessageTypes.EnrollResponse,
                    new EnrollResponse(result.AgentId, result.Secret, ""), ctx.RequestAborted);
            }
            else if (type == AgentMessageTypes.Heartbeat)
            {
                // auth via Authorization header instead
                var authHeader = ctx.Request.Headers.Authorization.ToString();
                if (!authHeader.StartsWith("Bearer ")) { await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "missing creds", default); return Results.Empty; }
                var parts = authHeader.Substring(7).Split(':');
                if (parts.Length != 2 || !Guid.TryParse(parts[0], out agentId))
                { await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "bad creds", default); return Results.Empty; }
                var secretHash = SHA256.HashData(Encoding.UTF8.GetBytes(parts[1]));
                var agent = await db.Agents.FindAsync(agentId);
                if (agent is null || !agent.SecretHash.SequenceEqual(secretHash))
                { await ws.CloseAsync(WebSocketCloseStatus.PolicyViolation, "auth failed", default); return Results.Empty; }
            }
            else
            {
                await ws.CloseAsync(WebSocketCloseStatus.InvalidMessageType, "unexpected first frame", default);
                return Results.Empty;
            }

            await registry.RegisterAsync(agentId, channel);
            var node = await db.Nodes.FirstAsync(n => n.Id == (await db.Agents.FindAsync(agentId))!.NodeId);
            node.Status = Domain.Nodes.NodeStatus.Active;
            node.LastSeenAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();

            try
            {
                while (ws.State == WebSocketState.Open)
                {
                    var r = await ws.ReceiveAsync(buf, ctx.RequestAborted);
                    if (r.MessageType == WebSocketMessageType.Close) break;
                    using var msg = JsonDocument.Parse(Encoding.UTF8.GetString(buf, 0, r.Count));
                    var msgType = msg.RootElement.GetProperty("Type").GetString();
                    if (msgType == AgentMessageTypes.Heartbeat)
                    {
                        node.LastSeenAt = DateTimeOffset.UtcNow;
                        await db.SaveChangesAsync();
                        await channel.SendJsonAsync(AgentMessageTypes.HeartbeatAck, new HeartbeatAck(0), ctx.RequestAborted);
                    }
                }
            }
            catch (Exception ex) { logger.LogWarning(ex, "Agent WS loop exited"); }
            finally
            {
                registry.Unregister(agentId);
                node.Status = Domain.Nodes.NodeStatus.Disconnected;
                await db.SaveChangesAsync();
            }
            return Results.Empty;
        });
        return app;
    }
}
```

- [ ] **Step 3: Wire into `Program.cs`**

Add `builder.Services.AddWebSocketManager();` is not a thing — instead add:
```csharp
app.UseWebSockets();
app.MapAgentsWebSocket();
```

- [ ] **Step 4: Commit**

```bash
git add src/
git commit -m "feat(ws): /api/agents/ws endpoint with enrollment + heartbeat loop"
```

---

### Task 7: Nodes UI in Angular

**Files:**
- Create: `limen/src/Limen.Frontend/src/app/features/nodes/nodes.component.ts`
- Create: `limen/src/Limen.Frontend/src/app/features/nodes/node-detail.component.ts`
- Create: `limen/src/Limen.Frontend/src/app/features/nodes/nodes.service.ts`
- Modify: `limen/src/Limen.Frontend/src/app/app.routes.ts`

- [ ] **Step 1: Add `ListNodesQuery` + endpoint**

```csharp
// src/Limen.Application/Queries/Nodes/ListNodesQuery.cs
using Limen.Application.Common.Interfaces;
using Limen.Domain.Nodes;
using Mediator;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Queries.Nodes;

public sealed record NodeDto(Guid Id, string Name, string[] Roles, string Status, DateTimeOffset? LastSeenAt);
public sealed record ListNodesQuery() : IQuery<IReadOnlyList<NodeDto>>;

internal sealed class ListNodesQueryHandler : IQueryHandler<ListNodesQuery, IReadOnlyList<NodeDto>>
{
    private readonly IAppDbContext _db;
    public ListNodesQueryHandler(IAppDbContext db) => _db = db;

    public async ValueTask<IReadOnlyList<NodeDto>> Handle(ListNodesQuery q, CancellationToken ct)
        => await _db.Nodes.Select(n => new NodeDto(n.Id, n.Name, n.Roles, n.Status.ToString(), n.LastSeenAt))
            .ToListAsync(ct);
}
```

Add `/api/nodes` endpoint in a new `NodesEndpoints.cs`:
```csharp
app.MapGet("/api/nodes", async (IMediator m) => Results.Ok(await m.Send(new ListNodesQuery())));
app.MapPost("/api/nodes/provisioning-keys", async (IMediator m, string[] roles) =>
    Results.Ok(await m.Send(new CreateProvisioningKeyCommand(roles))));
```

- [ ] **Step 2: Angular nodes service + component**

```ts
// src/app/features/nodes/nodes.service.ts
import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';

export interface NodeDto { id: string; name: string; roles: string[]; status: string; lastSeenAt?: string; }
export interface ProvisioningKeyResult { id: string; plaintextKey: string; expiresAt: string; }

@Injectable({ providedIn: 'root' })
export class NodesService {
  constructor(private http: HttpClient) {}
  list() { return this.http.get<NodeDto[]>('/api/nodes'); }
  createKey(roles: string[]) {
    return this.http.post<ProvisioningKeyResult>('/api/nodes/provisioning-keys', roles);
  }
}
```

```ts
// src/app/features/nodes/nodes.component.ts
import { Component, OnInit, inject, signal } from '@angular/core';
import { NodesService, NodeDto, ProvisioningKeyResult } from './nodes.service';

@Component({
  selector: 'limen-nodes',
  standalone: true,
  template: `
    <div class="p-8">
      <h1 class="text-3xl font-bold mb-6">Nodes</h1>
      <button (click)="createKey()" class="px-4 py-2 bg-slate-900 text-white rounded mb-4">Add node</button>
      @if (key()) {
        <div class="p-4 bg-amber-50 border border-amber-300 rounded mb-4">
          <p class="text-sm">Run on your new host (expires {{ key()!.expiresAt }}):</p>
          <pre class="mt-2 p-2 bg-white text-xs rounded">LIMEN_PROVISIONING_KEY={{ key()!.plaintextKey }}
LIMEN_ROLES=docker
docker compose up -d</pre>
        </div>
      }
      <table class="w-full">
        <thead><tr><th class="text-left">Name</th><th>Roles</th><th>Status</th><th>Last seen</th></tr></thead>
        <tbody>
          @for (n of nodes(); track n.id) {
            <tr><td>{{ n.name }}</td><td>{{ n.roles.join(', ') }}</td><td>{{ n.status }}</td><td>{{ n.lastSeenAt }}</td></tr>
          }
        </tbody>
      </table>
    </div>`,
})
export class NodesComponent implements OnInit {
  private svc = inject(NodesService);
  nodes = signal<NodeDto[]>([]);
  key = signal<ProvisioningKeyResult | null>(null);
  ngOnInit() { this.refresh(); setInterval(() => this.refresh(), 5000); }
  refresh() { this.svc.list().subscribe(x => this.nodes.set(x)); }
  createKey() { this.svc.createKey(['docker']).subscribe(x => this.key.set(x)); }
}
```

Add route:
```ts
{ path: 'nodes', loadComponent: () => import('./features/nodes/nodes.component').then(m => m.NodesComponent), canActivate: [authGuard] },
```

- [ ] **Step 3: Commit**

```bash
git add src/
git commit -m "feat(nodes): ListNodesQuery, provisioning-key creation endpoint, Nodes UI with polling"
```

---

### Task 8: Scaffold `limentinus` repo

From here on, work happens in the new `limentinus` repo.

- [ ] **Step 1: Create repo directory and init**

```bash
cd C:/GithubProjects/getlimen
# limentinus/ already exists with boilerplate; cd into it
cd limentinus
git init
```

- [ ] **Step 2: Copy Directory.Build.props, .gitignore, .editorconfig from `limen` (same boilerplate)**

- [ ] **Step 3: Create solution + projects**

```bash
dotnet new slnx -n Limentinus
dotnet new classlib -n Limentinus.Domain -o src/Limentinus.Domain
dotnet new classlib -n Limentinus.Application -o src/Limentinus.Application
dotnet new classlib -n Limentinus.Infrastructure -o src/Limentinus.Infrastructure
dotnet new worker -n Limentinus.API -o src/Limentinus.API
dotnet new xunit -n Limentinus.Tests -o src/Limentinus.Tests

dotnet sln Limentinus.slnx add src/Limentinus.Domain/Limentinus.Domain.csproj
dotnet sln Limentinus.slnx add src/Limentinus.Application/Limentinus.Application.csproj
dotnet sln Limentinus.slnx add src/Limentinus.Infrastructure/Limentinus.Infrastructure.csproj
dotnet sln Limentinus.slnx add src/Limentinus.API/Limentinus.API.csproj
dotnet sln Limentinus.slnx add src/Limentinus.Tests/Limentinus.Tests.csproj
```

- [ ] **Step 4: Enable NativeAOT on Limentinus.API**

Edit `src/Limentinus.API/Limentinus.API.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk.Worker">
  <PropertyGroup>
    <PublishAot>true</PublishAot>
    <InvariantGlobalization>true</InvariantGlobalization>
    <StripSymbols>true</StripSymbols>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="..\Limentinus.Application\Limentinus.Application.csproj" />
    <ProjectReference Include="..\Limentinus.Infrastructure\Limentinus.Infrastructure.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="10.0.0" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="9.0.0" />
    <PackageReference Include="Serilog.Sinks.Console" Version="6.0.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Reference Limen.Contracts (via NuGet when available; for v1 local prj ref)**

For development: reference the local `limen/contracts/Limen.Contracts/` project. In production: publish Limen.Contracts to a feed and reference by version.

Add to each project that needs contracts:
```xml
<ProjectReference Include="..\..\..\limen\contracts\Limen.Contracts\Limen.Contracts.csproj" />
```

(Future improvement: publish Limen.Contracts as a NuGet package from `limen`'s CI to GHCR, consume by version in other repos.)

- [ ] **Step 6: Commit**

```bash
git add .
git commit -m "chore: scaffold Limentinus solution (Domain, Application, Infrastructure, API worker, Tests)"
```

---

### Task 9: Limentinus identity persistence + WebSocket client

**Files:**
- Create: `limentinus/src/Limentinus.Domain/Node/NodeIdentity.cs`
- Create: `limentinus/src/Limentinus.Application/Services/EnrollmentService.cs`
- Create: `limentinus/src/Limentinus.Infrastructure/Control/LimenWebSocketChannel.cs`
- Create: `limentinus/src/Limentinus.API/Program.cs`

- [ ] **Step 1: `NodeIdentity`**

```csharp
// src/Limentinus.Domain/Node/NodeIdentity.cs
namespace Limentinus.Domain.Node;

public sealed class NodeIdentity
{
    public Guid AgentId { get; set; }
    public string Secret { get; set; } = string.Empty;
}
```

- [ ] **Step 2: `EnrollmentService` (application service, not a command)**

```csharp
// src/Limentinus.Application/Services/EnrollmentService.cs
using System.Text.Json;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Domain.Node;

namespace Limentinus.Application.Services;

public sealed class EnrollmentService
{
    private readonly IIdentityStore _store;
    private readonly ILimenControlClient _client;

    public EnrollmentService(IIdentityStore store, ILimenControlClient client) { _store = store; _client = client; }

    public async Task<NodeIdentity> EnsureEnrolledAsync(
        string provisioningKey, string hostname, string[] roles, string platform, string version, CancellationToken ct)
    {
        var existing = await _store.LoadAsync(ct);
        if (existing is not null) return existing;
        var id = await _client.EnrollAsync(provisioningKey, hostname, roles, platform, version, ct);
        await _store.SaveAsync(id, ct);
        return id;
    }
}
```

Interfaces:
```csharp
// src/Limentinus.Application/Common/Interfaces/IIdentityStore.cs
using Limentinus.Domain.Node;
namespace Limentinus.Application.Common.Interfaces;

public interface IIdentityStore
{
    Task<NodeIdentity?> LoadAsync(CancellationToken ct);
    Task SaveAsync(NodeIdentity id, CancellationToken ct);
}

public interface ILimenControlClient
{
    Task<NodeIdentity> EnrollAsync(string key, string hostname, string[] roles, string platform, string version, CancellationToken ct);
    Task RunAsync(NodeIdentity id, CancellationToken ct);  // maintains persistent WS with heartbeats
}
```

- [ ] **Step 3: `LimenWebSocketChannel` (Infrastructure)**

```csharp
// src/Limentinus.Infrastructure/Control/LimenWebSocketChannel.cs
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Limen.Contracts.AgentMessages;
using Limen.Contracts.Common;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Domain.Node;
using Microsoft.Extensions.Logging;

namespace Limentinus.Infrastructure.Control;

public sealed class LimenWebSocketChannel : ILimenControlClient
{
    private readonly Uri _serverUri;
    private readonly ILogger<LimenWebSocketChannel> _log;

    public LimenWebSocketChannel(Uri serverUri, ILogger<LimenWebSocketChannel> log) { _serverUri = serverUri; _log = log; }

    public async Task<NodeIdentity> EnrollAsync(string key, string hostname, string[] roles, string platform, string version, CancellationToken ct)
    {
        using var ws = new ClientWebSocket();
        await ws.ConnectAsync(new Uri(_serverUri, "/api/agents/ws"), ct);
        var env = new Envelope<EnrollRequest>(AgentMessageTypes.Enroll, ConfigVersion.Zero,
            new EnrollRequest(key, hostname, roles, platform, version));
        await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(env), WebSocketMessageType.Text, true, ct);

        var buf = new byte[16 * 1024];
        var r = await ws.ReceiveAsync(buf, ct);
        using var doc = JsonDocument.Parse(Encoding.UTF8.GetString(buf, 0, r.Count));
        var resp = doc.RootElement.GetProperty("Payload").Deserialize<EnrollResponse>()!;
        await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "enroll-complete", ct);
        return new NodeIdentity { AgentId = resp.AgentId, Secret = resp.PermanentSecret };
    }

    public async Task RunAsync(NodeIdentity id, CancellationToken ct)
    {
        var backoff = TimeSpan.FromSeconds(1);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var ws = new ClientWebSocket();
                ws.Options.SetRequestHeader("Authorization", $"Bearer {id.AgentId}:{id.Secret}");
                await ws.ConnectAsync(new Uri(_serverUri, "/api/agents/ws"), ct);
                backoff = TimeSpan.FromSeconds(1);
                // send initial heartbeat to satisfy first-frame contract
                await SendAsync(ws, AgentMessageTypes.Heartbeat, new Heartbeat(DateTimeOffset.UtcNow, Array.Empty<string>()), ct);

                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    await Task.Delay(TimeSpan.FromSeconds(30), ct);
                    await SendAsync(ws, AgentMessageTypes.Heartbeat, new Heartbeat(DateTimeOffset.UtcNow, Array.Empty<string>()), ct);
                }
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _log.LogWarning(ex, "WS connection lost; reconnecting in {Backoff}", backoff);
                await Task.Delay(backoff, ct);
                backoff = TimeSpan.FromSeconds(Math.Min(60, backoff.TotalSeconds * 2));
            }
        }
    }

    private static async Task SendAsync<T>(ClientWebSocket ws, string type, T payload, CancellationToken ct)
    {
        var env = new Envelope<T>(type, ConfigVersion.Zero, payload);
        await ws.SendAsync(JsonSerializer.SerializeToUtf8Bytes(env), WebSocketMessageType.Text, true, ct);
    }
}
```

- [ ] **Step 4: Implement `FileIdentityStore`**

```csharp
// src/Limentinus.Infrastructure/Persistence/FileIdentityStore.cs
using System.Text.Json;
using Limentinus.Application.Common.Interfaces;
using Limentinus.Domain.Node;

namespace Limentinus.Infrastructure.Persistence;

public sealed class FileIdentityStore : IIdentityStore
{
    private readonly string _path;
    public FileIdentityStore(string path) => _path = path;

    public async Task<NodeIdentity?> LoadAsync(CancellationToken ct)
    {
        if (!File.Exists(_path)) return null;
        var txt = await File.ReadAllTextAsync(_path, ct);
        return JsonSerializer.Deserialize<NodeIdentity>(txt);
    }

    public async Task SaveAsync(NodeIdentity id, CancellationToken ct)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(id), ct);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(_path, UnixFileMode.UserRead | UnixFileMode.UserWrite);  // 0600
    }
}
```

- [ ] **Step 5: Write `Program.cs`**

```csharp
// src/Limentinus.API/Program.cs
using Limentinus.Application.Common.Interfaces;
using Limentinus.Application.Services;
using Limentinus.Infrastructure.Control;
using Limentinus.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var host = Host.CreateApplicationBuilder(args);
host.Services.AddSerilog();

var limenUrl = Environment.GetEnvironmentVariable("LIMEN_CENTRAL_URL")
    ?? throw new InvalidOperationException("LIMEN_CENTRAL_URL required");
var hostname = Environment.MachineName;
var roles = (Environment.GetEnvironmentVariable("LIMEN_ROLES") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
var provisioningKey = Environment.GetEnvironmentVariable("LIMEN_PROVISIONING_KEY");
var identityPath = Environment.GetEnvironmentVariable("LIMEN_IDENTITY_PATH") ?? "/var/lib/limentinus/identity.json";

host.Services.AddSingleton<IIdentityStore>(_ => new FileIdentityStore(identityPath));
host.Services.AddSingleton<ILimenControlClient>(sp =>
    new LimenWebSocketChannel(new Uri(limenUrl), sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<LimenWebSocketChannel>>()));
host.Services.AddSingleton<EnrollmentService>();

host.Services.AddHostedService<AgentWorker>();

await host.Build().RunAsync();

public sealed class AgentWorker : BackgroundService
{
    private readonly EnrollmentService _enroll;
    private readonly ILimenControlClient _client;
    private readonly string[] _roles;
    private readonly string _hostname;
    private readonly string? _provisioningKey;

    public AgentWorker(EnrollmentService enroll, ILimenControlClient client, IHostApplicationLifetime _)
    {
        _enroll = enroll; _client = client;
        _roles = (Environment.GetEnvironmentVariable("LIMEN_ROLES") ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries);
        _hostname = Environment.MachineName;
        _provisioningKey = Environment.GetEnvironmentVariable("LIMEN_PROVISIONING_KEY");
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var id = await _enroll.EnsureEnrolledAsync(_provisioningKey ?? "", _hostname, _roles,
            Environment.OSVersion.Platform.ToString(), "0.1.0", ct);
        await _client.RunAsync(id, ct);
    }
}
```

- [ ] **Step 6: Dockerfile for Limentinus**

```dockerfile
# src/Limentinus.API/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Limentinus.API/Limentinus.API.csproj -c Release -r linux-x64 -o /app \
    /p:PublishAot=true /p:StripSymbols=true

FROM mcr.microsoft.com/dotnet/runtime-deps:10.0
WORKDIR /app
COPY --from=build /app/Limentinus.API .
VOLUME ["/var/lib/limentinus"]
ENTRYPOINT ["./Limentinus.API"]
```

- [ ] **Step 7: compose.yml**

```yaml
# limentinus/compose.yml
name: limentinus
services:
  limentinus:
    image: ghcr.io/getlimen/limentinus:latest
    restart: unless-stopped
    environment:
      LIMEN_CENTRAL_URL: ${LIMEN_CENTRAL_URL}
      LIMEN_ROLES: ${LIMEN_ROLES:-docker}
      LIMEN_PROVISIONING_KEY: ${LIMEN_PROVISIONING_KEY:-}
    volumes:
      - limentinus_state:/var/lib/limentinus
volumes:
  limentinus_state:
```

- [ ] **Step 8: Build, commit, push**

```bash
dotnet build
git add .
git commit -m "feat: Limentinus worker with enrollment + WS heartbeat loop"
```

---

### Task 10: End-to-end smoke test

- [ ] **Step 1: Start Limen locally**

```bash
cd limen
docker compose -f compose.dev.yml up -d
cd src/Limen.API && dotnet run
```

- [ ] **Step 2: Sign in, get provisioning key**

Navigate to UI, click "Add node", copy the plaintext key.

- [ ] **Step 3: Run Limentinus in another terminal**

```bash
cd limentinus
LIMEN_CENTRAL_URL=ws://localhost:5000 \
LIMEN_ROLES=docker \
LIMEN_PROVISIONING_KEY=<paste-key> \
LIMEN_IDENTITY_PATH=./test-identity.json \
dotnet run --project src/Limentinus.API
```

- [ ] **Step 4: Verify UI shows node as `Active`**

Refresh the nodes page in the browser; new node should appear with status `Active` and `LastSeenAt` updating every 30s.

- [ ] **Step 5: Kill Limentinus, observe status drops to `Disconnected`**

- [ ] **Step 6: Restart Limentinus — reconnects using persisted identity, status back to `Active`**

- [ ] **Step 7: Commit final docs + push**

```bash
git add .
git commit -m "docs: Plan 2 complete — agent enrollment + WS heartbeat end-to-end"
git push
```

---

## Exit criteria for Plan 2

✅ `limentinus` repo scaffolded with clean architecture + NativeAOT
✅ Provisioning key flow: create → one-shot → burns on enroll
✅ Agent identity persisted locally (0600)
✅ JSON/WS channel with auto-reconnect (1s → 60s backoff)
✅ Node status (Pending/Active/Disconnected) updated in UI
✅ Heartbeat every 30s visible in `lastSeenAt`
✅ All tests pass

**Plan 3 unlocks next:** Forculus WG hub + tunneled traffic.
