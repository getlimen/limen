# Plan 04 — `ostiarius`: custom reverse proxy + Let's Encrypt

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:subagent-driven-development` or `superpowers:executing-plans`.

**Goal:** Scaffold the `ostiarius` repo. YARP-based reverse proxy with automatic TLS via LettuceEncrypt-Archon. Limen pushes route + auth-policy updates over WebSocket. Admin can add a Service + a Route pointing at it; traffic routes through WG tunnel to the backend. No auth gate yet (comes in Plan 6).

**Architecture:** ASP.NET Core with YARP `IProxyConfigProvider` (in-memory). LettuceEncrypt-Archon handles ACME HTTP-01. Ostiarius opens a JSON/WS to Limen (same pattern as Limentinus) to receive `ApplyRouteSetCommand` messages.

**Prerequisites:** Plans 1-3 complete. Nodes with `proxy` role exist and have WG tunnels.

---

## File structure

**`limen` repo (manager side):**
- `contracts/Limen.Contracts/ProxyMessages/{ApplyRouteSet.cs, RouteSpec.cs, ProxyMessageTypes.cs}`
- `src/Limen.Domain/{Services/Service.cs, Routes/PublicRoute.cs}`
- `src/Limen.Application/Commands/Services/{CreateServiceCommand.cs, UpdateServiceCommand.cs, DeleteServiceCommand.cs}`
- `src/Limen.Application/Commands/Routes/{AddRouteCommand.cs, RemoveRouteCommand.cs, AssignRouteToProxyCommand.cs}`
- `src/Limen.Application/Queries/{Services/*, Routes/*}`
- `src/Limen.Application/Services/ProxyConfigPusher.cs`
- `src/Limen.Infrastructure/Proxies/{ProxyWebSocketHandler.cs, ProxyConnectionRegistry.cs}`
- `src/Limen.API/Endpoints/{ServicesEndpoints.cs, RoutesEndpoints.cs, ProxiesWebSocketEndpoint.cs}`
- Frontend: `src/Limen.Frontend/src/app/features/services/*`, `routes/*`

**`ostiarius` repo (new):**
- Full clean-arch scaffold + NativeAOT
- `src/Ostiarius.Domain/Certificates/CertificateRecord.cs`
- `src/Ostiarius.Application/Services/{RouteApplicationService.cs, CertificateRenewalService.cs}`
- `src/Ostiarius.Application/Commands/Routes/{ApplyRouteSetCommand.cs, UpsertRouteCommand.cs}`
- `src/Ostiarius.Infrastructure/Proxy/{YarpConfigProvider.cs, RouteStore.cs}`
- `src/Ostiarius.Infrastructure/Acme/{LettuceEncryptIntegration.cs, FileCertificateStore.cs}`
- `src/Ostiarius.Infrastructure/Control/LimenWebSocketClient.cs`
- `src/Ostiarius.API/Program.cs` — Kestrel + YARP + control WS
- `Dockerfile`, `compose.yml`

---

## Tasks

### Task 1: Proxy contracts

```csharp
// contracts/Limen.Contracts/ProxyMessages/ProxyMessageTypes.cs
namespace Limen.Contracts.ProxyMessages;
public static class ProxyMessageTypes
{
    public const string ApplyRouteSet = "proxy/applyRouteSet";
    public const string RouteSetAck = "proxy/routeSetAck";
    public const string ProxyReady = "proxy/ready";
    public const string CertRenewed = "proxy/certRenewed";
}

// RouteSpec.cs
public sealed record RouteSpec(
    Guid RouteId,
    string Hostname,         // "app.example.com"
    string UpstreamUrl,      // "http://10.42.0.17:3000"
    bool TlsEnabled,
    string AuthPolicy);      // "none" for now; later "password"/"sso"/"allowlist"

// ApplyRouteSet.cs
public sealed record ApplyRouteSet(IReadOnlyList<RouteSpec> Routes);
public sealed record RouteSetAck(ulong AppliedVersion, int RouteCount);
```

### Task 2: Domain — Service, PublicRoute

```csharp
// src/Limen.Domain/Services/Service.cs
public class Service
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid TargetNodeId { get; set; }         // docker-role node running this service
    public string ContainerName { get; set; } = string.Empty;
    public int InternalPort { get; set; }
    public string Image { get; set; } = string.Empty;  // "ghcr.io/foo/bar:latest"
    public bool AutoDeploy { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}

// src/Limen.Domain/Routes/PublicRoute.cs
public class PublicRoute
{
    public Guid Id { get; set; }
    public Guid ServiceId { get; set; }
    public Guid ProxyNodeId { get; set; }      // proxy-role node serving this hostname
    public string Hostname { get; set; } = string.Empty;
    public bool TlsEnabled { get; set; } = true;
    public string AuthPolicy { get; set; } = "none";
    public DateTimeOffset CreatedAt { get; set; }
}
```

EF configs, DbSets, migration `ServicesAndRoutes`.

### Task 3: CQRS — CreateServiceCommand, AddRouteCommand

One file per command (clean-arch rule). Include handlers. Validate upstream inputs with FluentValidation (hostname regex, ports in range, node exists with proper role).

Add queries: `ListServicesQuery`, `GetServiceByIdQuery`, `ListRoutesQuery`. Standard scaffolding.

Add endpoints: `/api/services`, `/api/routes`.

### Task 4: ProxyConfigPusher

```csharp
// src/Limen.Application/Services/ProxyConfigPusher.cs
using Limen.Application.Common.Interfaces;
using Limen.Contracts.ProxyMessages;
using Microsoft.EntityFrameworkCore;

namespace Limen.Application.Services;

public sealed class ProxyConfigPusher
{
    private readonly IAppDbContext _db;
    private readonly IProxyConnectionRegistry _registry;

    public ProxyConfigPusher(IAppDbContext db, IProxyConnectionRegistry registry)
    { _db = db; _registry = registry; }

    public async Task PushFullAsync(Guid proxyNodeId, CancellationToken ct)
    {
        var routes = await _db.PublicRoutes
            .Where(r => r.ProxyNodeId == proxyNodeId)
            .Join(_db.Services, r => r.ServiceId, s => s.Id, (r, s) => new { r, s })
            .Join(_db.WireGuardPeers, x => x.s.TargetNodeId, p => p.AgentId, (x, p) => new { x.r, x.s, p })
            .ToListAsync(ct);

        var specs = routes.Select(x => new RouteSpec(
            x.r.Id, x.r.Hostname,
            $"http://{x.p.TunnelIp.Split('/')[0]}:{x.s.InternalPort}",
            x.r.TlsEnabled, x.r.AuthPolicy)).ToList();

        var chan = _registry.Get(proxyNodeId);
        if (chan is null) return;  // offline; will catch up on reconnect
        await chan.SendJsonAsync(ProxyMessageTypes.ApplyRouteSet, new ApplyRouteSet(specs), ct);
    }
}
```

Hook into `AddRouteCommand` and `RemoveRouteCommand` handlers to call `PushFullAsync` after persistence.

### Task 5: ProxyConnectionRegistry + WebSocket endpoint

Mirror `AgentConnectionRegistry` and `AgentsWebSocketEndpoint` from Plan 2, but under `/api/proxies/ws` — authenticating with proxy-node credentials (proxy role agents share the same enrollment as nodes; in Plan 2's model, an agent with `proxy` role is also a valid credentials holder here).

Actually simplification: Ostiarius uses the same agent credentials as Limentinus on that host. The `proxy` role flag on the agent means: when this agent connects, Limen knows to expect an Ostiarius WS on `/api/proxies/ws` as well. Both streams auth with same creds.

### Task 6: Scaffold `ostiarius` repo

```bash
cd C:/GithubProjects/getlimen/ostiarius
git init
# standard clean-arch 5-project setup, AOT on Ostiarius.API
```

### Task 7: RouteStore + YARP config provider

```csharp
// src/Ostiarius.Infrastructure/Proxy/RouteStore.cs
using System.Collections.Concurrent;
using Limen.Contracts.ProxyMessages;

namespace Ostiarius.Infrastructure.Proxy;

public sealed class RouteStore
{
    private readonly ConcurrentDictionary<Guid, RouteSpec> _routes = new();
    public event Action? Changed;

    public void ReplaceAll(IEnumerable<RouteSpec> routes)
    {
        _routes.Clear();
        foreach (var r in routes) _routes[r.RouteId] = r;
        Changed?.Invoke();
    }

    public IReadOnlyCollection<RouteSpec> Snapshot() => _routes.Values.ToArray();
}
```

```csharp
// src/Ostiarius.Infrastructure/Proxy/YarpConfigProvider.cs
using Yarp.ReverseProxy.Configuration;

namespace Ostiarius.Infrastructure.Proxy;

public sealed class YarpConfigProvider : IProxyConfigProvider
{
    private InMemoryConfig _config;
    private readonly RouteStore _store;

    public YarpConfigProvider(RouteStore store)
    {
        _store = store;
        _config = Build();
        store.Changed += () => { var old = _config; _config = Build(); old.SignalChange(); };
    }

    public IProxyConfig GetConfig() => _config;

    private InMemoryConfig Build()
    {
        var routes = _store.Snapshot().Select(r => new RouteConfig
        {
            RouteId = r.RouteId.ToString(),
            ClusterId = $"cluster-{r.RouteId}",
            Match = new RouteMatch { Hosts = new[] { r.Hostname } },
        }).ToList();

        var clusters = _store.Snapshot().Select(r => new ClusterConfig
        {
            ClusterId = $"cluster-{r.RouteId}",
            Destinations = new Dictionary<string, DestinationConfig>
            {
                ["dest1"] = new() { Address = r.UpstreamUrl }
            }
        }).ToList();

        return new InMemoryConfig(routes, clusters);
    }

    private sealed class InMemoryConfig : IProxyConfig
    {
        private readonly CancellationTokenSource _cts = new();
        public InMemoryConfig(IReadOnlyList<RouteConfig> routes, IReadOnlyList<ClusterConfig> clusters)
        { Routes = routes; Clusters = clusters; ChangeToken = new Microsoft.Extensions.Primitives.CancellationChangeToken(_cts.Token); }
        public IReadOnlyList<RouteConfig> Routes { get; }
        public IReadOnlyList<ClusterConfig> Clusters { get; }
        public Microsoft.Extensions.Primitives.IChangeToken ChangeToken { get; }
        public void SignalChange() => _cts.Cancel();
    }
}
```

### Task 8: Program.cs wiring YARP + LettuceEncrypt-Archon + control WS

```csharp
// src/Ostiarius.API/Program.cs
using Ostiarius.Application.Services;
using Ostiarius.Infrastructure.Acme;
using Ostiarius.Infrastructure.Control;
using Ostiarius.Infrastructure.Proxy;
using LettuceEncrypt;  // Archon fork
using Serilog;
using Yarp.ReverseProxy.Configuration;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateBootstrapLogger();

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((_, cfg) => cfg.WriteTo.Console());

builder.Services.AddSingleton<RouteStore>();
builder.Services.AddSingleton<IProxyConfigProvider, YarpConfigProvider>();
builder.Services.AddReverseProxy();

builder.Services.AddLettuceEncrypt(opts =>
{
    opts.AcceptTermsOfService = true;
    opts.DomainNames = Array.Empty<string>();  // dynamically added via RouteStore
    opts.EmailAddress = builder.Configuration["Acme:Email"];
}).PersistDataToDirectory(new DirectoryInfo("/data/certs"), null);

builder.Services.AddHostedService<LimenWebSocketClient>();
builder.Services.AddHostedService<CertificateRenewalService>();

var app = builder.Build();
app.MapReverseProxy();
app.Run();
```

### Task 9: LimenWebSocketClient — receives ApplyRouteSet from Limen

Mirror `LimenWebSocketChannel` from Limentinus. On `ApplyRouteSet`, call `RouteStore.ReplaceAll(spec.Routes)` and send `RouteSetAck` back.

### Task 10: CertificateRenewalService

BackgroundService scanning `/data/certs` every 6 hours, using LettuceEncrypt's renewal API. Send `CertRenewed` event back to Limen via WS on success.

### Task 11: Dockerfile + compose

```dockerfile
# src/Ostiarius.API/Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/Ostiarius.API/Ostiarius.API.csproj -c Release -r linux-x64 -o /app /p:PublishAot=true

FROM alpine:3.19
RUN apk add --no-cache icu-libs
WORKDIR /app
COPY --from=build /app .
EXPOSE 80 443
VOLUME ["/data/certs"]
ENTRYPOINT ["./Ostiarius.API"]
```

Limentinus on a `proxy`-role node brings up Ostiarius as a sibling container (adds to local compose). Admin doesn't run Ostiarius directly.

### Task 12: UI — Services + Routes pages in Angular

- `services.component.ts`: list + create (form: name, image, target node, internal port)
- `routes.component.ts`: list + create (form: hostname, service, proxy node, TLS enabled)
- DNS hint displayed: "point `<hostname>` at `<proxy-node-public-ip>`"

### Task 13: E2E smoke test

1. Enroll two nodes: A (`docker,control,proxy`), B (`docker`)
2. On B, deploy a simple nginx container (via `docker run` outside Limen for now — full deploy comes in Plan 5)
3. In Limen UI: create a Service pointing at B's nginx (port 80), add a Route `nginx.local` → service via proxy node A
4. Edit `/etc/hosts` to map `nginx.local` to A's public IP
5. `curl https://nginx.local` (accept LE staging cert for testing)
6. Verify nginx responds

Commit + push.

---

## Exit criteria for Plan 4

✅ `ostiarius` repo scaffolded
✅ YARP routing live, reloadable via RouteStore
✅ LettuceEncrypt-Archon issuing certs on HTTP-01
✅ Limen WS push updates routes in real time
✅ Services + Routes UI in Angular
✅ E2E test: hit a public hostname, traffic tunnels through WG to backend

**Plan 5 unlocks next:** Docker deploys + auto-update polling.
