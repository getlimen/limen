# Limen — Design Spec

**Date:** 2026-04-14
**Status:** Draft, pending user review
**Author:** Niclas (PianoNic)

---

## 1. Overview

**Limen** *(/ˈliː.mən/, Latin: "threshold")* is a self-hosted infrastructure platform that combines three capabilities that normally require three separate tools:

1. **Docker deploy management** — auto-deploy services from registry image updates (like Coolify)
2. **Reverse proxy with TLS** — public ingress, routing, Let's Encrypt (like Traefik/Caddy fronting Coolify)
3. **WireGuard hub-and-spoke VPN** — connect remote sites/servers without public inbound ports (like Pangolin)

The unifying idea: every request, packet, and deploy crosses a threshold the platform controls. A single admin manages all three capabilities through one Angular UI on top of a C# control plane.

> **Two identity planes (don't confuse these):**
> - **Admin identity** — the person running Limen. Exactly one in v1, authenticates via OIDC. Manages nodes, services, routes.
> - **Resource identity** — users hitting resources Limen exposes publicly (e.g., someone opening `app.example.com`). Controlled per-resource by an auth mode (`none` / `password` / `sso` / `allowlist`). See §10. Not "accounts" in a user-management sense — just credentials per resource.

> **Etymology:** *Limen* is the Latin word for the stone at the bottom of a Roman doorway — the line between inside and outside. Limen sits at every threshold of your infrastructure: the line between public internet and private services (reverse proxy), outside networks and internal mesh (WireGuard), and user intent and container execution (Docker orchestration). (See also: *liminal*, *subliminal*, *eliminate* — all derive from this root.)

## 2. Scope

### 2.1 — v1 in-scope

- Hub-and-spoke WireGuard control plane (central relay + remote agents)
- Per-service reverse proxy with automatic TLS (Let's Encrypt)
- Docker deploy management: pull image, start/stop containers, health-check, rollback
- Auto-deploy triggered by new registry image digests
- Resource-level authentication (password / SSO / email allowlist)
- Role-based node model: `control` / `docker` / `proxy` (any combination per node)
- Single-admin authentication via OIDC (provider-agnostic, default example uses Pocket ID)
- Angular UI at the control node; no CLI in v1

### 2.2 — v2+ roadmap (documented, deferred)

- Kubernetes / k3s support alongside Docker
- Multi-admin accounts with RBAC
- Config-as-code export/import (YAML) alongside UI
- CLI (`limen-cli`)
- HA for the control plane
- PIN / API token / WebAuthn auth modes for resources
- Enterprise path: SAML SSO, audit logs, BSL-licensed enterprise edition (current: Apache 2.0 community)
- Per-path auth rules (e.g., `/admin/*` requires SSO)
- Registry webhook support (alongside polling)

### 2.3 — Non-goals

- Hosting Limen as SaaS (self-hosted only in v1)
- Supporting arbitrary orchestrators beyond Docker (v1) / K8s (v2+)
- Acting as a build system (no buildpacks, no in-platform image building — Limen deploys pre-built images only)
- Replacing Traefik/Caddy/Nginx for users who already have them — Limen ships its own proxy (Ostiarius)

## 3. Locked decisions

| Domain | Decision |
|--------|----------|
| Project name | **Limen** |
| GitHub org | `github.com/getlimen` |
| Components | `limen`, `ostiarius`, `forculus`, `limentinus` (+ `limen-cli`, `limen-docs` later) |
| License | Apache 2.0 |
| Language (code/docs/commits) | English only |
| Backend runtime | .NET 10 / ASP.NET Core |
| Frontend | Angular 21 + spartan.ng + Tailwind CSS 4 |
| ORM | EF Core, code-first migrations |
| Database | **PostgreSQL (sole datastore)** — no Redis, no MinIO, no vector DB |
| Cache | `IMemoryCache` (in-process) |
| Pub/sub (if ever needed) | Postgres `LISTEN/NOTIFY` |
| Background jobs | Quartz.NET (Apache 2.0) |
| Agent/proxy binaries | C# NativeAOT |
| Reverse proxy engine | YARP + LettuceEncrypt-Archon, running inside Ostiarius (custom Limen-native proxy image) |
| WireGuard in Forculus | `wg` CLI subprocess (matches Gerbil pattern, adapted for C#) |
| WireGuard in Limentinus | Embedded `wireguard-go` userspace + UAPI socket |
| Admin auth | OIDC (provider-agnostic) |
| Resource auth modes v1 | `none`, `password`, `sso`, `allowlist` (magic link) |
| Deployment model | Docker compose only — no single-binary install, no K8s in v1 |
| Architecture style | Clean / onion (strict rules — see §5) |
| Control-plane ↔ agent protocol | JSON over WebSocket (agent-initiated, persistent, auto-reconnect) |
| Control-plane ↔ Forculus protocol | HTTP REST (push on change + poll on boot/reconcile) |
| Control-plane ↔ Ostiarius protocol | JSON over WebSocket (same pattern as agents) |

## 4. Architecture — topology

Four components, freely deployed across one or more hosts. Every host that Limen manages runs **Limentinus**; additional components deploy based on role flags (`control`, `docker`, `proxy`).

```
                        ┌──────────────────────────────────────────────────┐
                        │  Node A — roles: control,docker,proxy            │
                        │                                                  │
   admin (browser) ───► │  Limentinus ── loopback WS ──► Limen             │
                        │                                  │ HTTP REST     │
                        │                                  ▼               │
                        │                               Forculus ──┐       │
                        │                               (wg CLI)   │ WG    │
   public traffic ────► │                Ostiarius                 │       │
                        │                    │                     │       │
                        │                    └─── JSON/WS ──► Limen        │
                        │                                                  │
                        │              Postgres (sole datastore)           │
                        └──────────────────────────────────────────────────┘
                                                              │ WG tunnels
                                ┌─────────────────────────────┼────────────┐
                                │                             │            │
                    ┌───────────▼─────┐       ┌───────────────▼──┐    ┌───▼─────────┐
                    │ Node B          │       │ Node C           │    │ Node D      │
                    │ roles: proxy    │       │ roles: docker,   │    │ roles:      │
                    │                 │       │        proxy     │    │  docker     │
                    │ Limentinus ──┐  │       │ Limentinus ──┐   │    │ Limentinus  │
                    │  WS ▲        │  │       │  WS ▲        │   │    │  WS ▲       │
                    │     └→ Limen │  │       │     └→ Limen │   │    │     └→Limen │
                    │ Ostiarius    │  │       │ Ostiarius    │   │    │ (local      │
                    │  WS ──► Limen│  │       │  WS ──► Limen│   │    │  Docker)    │
                    │              ▼  │       │ Docker       ▼   │    │             │
                    │ wireguard-go tun│       │ wireguard-go tun │    │ wireguard-go│
                    └─────────────────┘       └──────────────────┘    └─────────────┘
```

### 4.1 — Component roles & Roman doorway theme

Every component takes its name from a Roman doorway concept or minor deity — Romans had a named figure for every part of a door.

| Component | Latin meaning | Role in Roman concept | Platform role |
|-----------|---------------|-----------------------|---------------|
| **Limen** | *threshold* (the stone you cross) | The boundary between inside and outside | Central manager — the platform itself |
| **Ostiarius** | *doorkeeper / porter* | Admits visitors, inspects, routes them | Reverse proxy — TLS termination + YARP routing |
| **Forculus** | god of the door panel | The physical gate you swing open | WireGuard hub — server-side WG endpoint |
| **Limentinus** | guardian-spirit of thresholds | Watches over each doorway | Node agent — runs on every managed host |

### 4.2 — Node roles (composable flags)

| Flag | Enables |
|------|---------|
| `control` | Limen + Forculus + Postgres on this node — the brain |
| `proxy` | Ostiarius on this node — public TLS terminates here |
| `docker` | Docker socket access — user services run here |

Typical combinations:

- **Single-node homelab:** `control,docker,proxy` on one machine
- **Split compute/edge:** Node A `control,docker` + Node B `proxy`
- **Dedicated control plane:** Node A `control` + Nodes B–E `docker,proxy`
- **Geo-distributed proxies:** Node A `control,docker` + B/C/D `proxy` in different regions

A host becomes a **node** only when Limentinus is installed on it. Unmanaged hosts are invisible to Limen.

## 5. Clean architecture — strict rules

All four components follow the same layered structure. The layer rules are strict:

- **Domain layer** — ONLY database entity models. No value objects, no enums, no services, no interfaces, no abstractions — just classes that map to DB tables. Components without a DB may have an empty Domain folder or hold entity-shaped types for local persisted state (e.g., Ostiarius cert records).
- **Infrastructure layer** — ALL DB code: EF Core, DbContext, migrations, repositories. Plus ALL external integrations: WebSocket/HTTP clients, OIDC, Quartz job definitions, Docker API, subprocess mgmt, Kestrel/YARP wiring, ACME.
- **Application layer** — EVERYTHING else: services, commands, queries, DTOs, validators, interfaces for infrastructure contracts, Mediator pipeline behaviors. All business logic.

### 5.1 — Application layer sub-structure

- Top-level `Commands/` and `Queries/` folders, capitalized.
- Under those, organize by feature/domain (e.g. `Commands/Nodes/`, `Queries/Services/`).

### 5.2 — Command/Query file convention

- **One file per command or query.** The file contains BOTH the command/query type AND its handler.
- Example: `CreateServiceCommand.cs` contains `CreateServiceCommand` record + `CreateServiceCommandHandler` class in the same file.
- Same rule for queries.
- **Do not split the command/query type and its handler into separate files.**

### 5.3 — Shared contracts project

All WebSocket and HTTP DTOs live in one standalone C# library, `Limen.Contracts`, published as a versioned NuGet from the `limen` repo. All four components reference it. Serialization via `System.Text.Json` source generators (AOT-safe).

```
contracts/
└── Limen.Contracts/
    ├── AgentMessages/        # Limen ↔ Limentinus (WS)
    ├── ProxyMessages/        # Limen ↔ Ostiarius (WS)
    ├── ForculusHttp/         # Limen ↔ Forculus (HTTP DTOs)
    └── Common/
        ├── ConfigVersion.cs  # monotonic u64 for dedup
        └── Envelope.cs       # { Type, Version, Payload }
```

## 6. Component internals

### 6.1 — `limen` (central manager)

**Role:** Owns all state in Postgres. Orchestrates via WebSocket to agents/proxies and HTTP to Forculus. Serves Angular UI. No public ingress — Ostiarius fronts it.

```
src/
├── Limen.Domain/
│   ├── Nodes/                 # Node, NodeCapability
│   ├── Services/              # Service, ServiceVersion
│   ├── Routes/                # PublicRoute
│   ├── Tunnels/               # WireGuardPeer
│   ├── Auth/                  # ResourceAuthPolicy, AllowlistedEmail, MagicLink, IssuedToken, OidcProvider
│   └── Deployments/           # DeploymentQueue
│
├── Limen.Application/
│   ├── Common/
│   │   ├── Behaviors/         # ValidationBehavior, LoggingBehavior
│   │   ├── Interfaces/        # IAgentConnection, IProxyConnection, IForculusClient, ITokenSigner
│   │   └── DTOs/
│   ├── Services/              # application orchestration services
│   │   ├── NodeEnrollmentService.cs
│   │   ├── DeploymentPlanner.cs
│   │   └── TunnelCoordinator.cs
│   ├── Commands/
│   │   ├── Nodes/             # EnrollNodeCommand, RotateNodeKeyCommand, …
│   │   ├── Services/          # CreateServiceCommand, DeployServiceCommand, …
│   │   ├── Routes/            # AddRouteCommand, AssignRouteToProxyCommand
│   │   ├── Auth/              # LoginWithPasswordCommand, InitiateMagicLinkCommand,
│   │   │                      # VerifyMagicLinkCommand, InitiateOidcCommand,
│   │   │                      # HandleOidcCallbackCommand, RevokeTokenCommand
│   │   └── Deployments/       # CreateDeploymentCommand, CancelDeploymentCommand
│   └── Queries/
│       ├── Nodes/             # ListNodesQuery, GetNodeByIdQuery
│       ├── Services/          # GetServiceByIdQuery
│       ├── Routes/
│       └── Deployments/       # ListDeploymentsQuery, GetDeploymentLogsQuery
│
├── Limen.Infrastructure/
│   ├── Persistence/           # AppDbContext, Configurations, Migrations, Repositories
│   ├── Agents/                # AgentWebSocketServer + per-connection sessions
│   ├── Proxies/               # ProxyWebSocketServer
│   ├── Tunnels/               # ForculusHttpClient
│   ├── Registry/              # RegistryClient (Docker Hub, GHCR, etc.)
│   ├── Auth/                  # OidcHandler, TokenSigner (Ed25519)
│   └── Jobs/                  # Quartz jobs: RegistryPollJob, TokenRevocationSweepJob, …
│
├── Limen.API/                 # ASP.NET Core entry; thin transport
│   ├── Endpoints/             # minimal-APIs or controllers
│   ├── WebSocketHandlers/     # /api/agents/ws, /api/proxies/ws
│   ├── Middleware/
│   └── Program.cs
│
├── Limen.Frontend/            # Angular 21 + spartan.ng + Tailwind 4
│   └── src/app/{core,shared,features,layout}
│
└── Limen.Tests/
```

### 6.2 — `ostiarius` (reverse proxy)

**Role:** Terminate public TLS, authenticate requests to protected routes, route HTTP to backend services through WG tunnel. Deployed on any `proxy`-role node.

```
src/
├── Ostiarius.Domain/
│   └── Certificates/
│       └── CertificateRecord.cs            # PEM + metadata, persisted to disk volume
│
├── Ostiarius.Application/
│   ├── Common/
│   │   └── Interfaces/                     # ICertificateStore, IRouteStore, IControlClient, IJwtVerifier, IRevokedTokenCache
│   ├── Services/
│   │   ├── CertificateRenewalService.cs    # BackgroundService (6h scan)
│   │   ├── RouteApplicationService.cs
│   │   └── SessionVerifier.cs              # Ed25519 verify + revocation check
│   ├── Commands/
│   │   ├── Routes/
│   │   │   ├── ApplyRouteSetCommand.cs
│   │   │   └── UpsertRouteCommand.cs
│   │   ├── Certificates/
│   │   │   └── RenewCertificateCommand.cs
│   │   └── Auth/
│   │       └── UpdateAuthPolicyCommand.cs  # from Limen push
│   └── Queries/
│       ├── Routes/ListActiveRoutesQuery.cs
│       └── Certificates/GetCertificateStatusQuery.cs
│
├── Ostiarius.Infrastructure/
│   ├── Proxy/
│   │   ├── YarpConfigProvider.cs           # implements IProxyConfigProvider
│   │   └── AuthMiddleware.cs               # runs before YARP routing
│   ├── Acme/
│   │   ├── LettuceEncryptIntegration.cs
│   │   └── FileCertificateStore.cs
│   ├── Control/
│   │   └── LimenWebSocketClient.cs         # JSON/WS bidi to Limen
│   ├── Auth/
│   │   ├── Ed25519Verifier.cs
│   │   └── RevokedTokenPoller.cs           # 30s poll of /api/auth/revoked
│   └── Hosting/
│       └── KestrelSetup.cs
│
├── Ostiarius.API/                          # Program.cs
└── Ostiarius.Tests/
```

### 6.3 — `forculus` (WireGuard hub)

**Role:** Server-side WireGuard endpoint. Accepts tunnel connections from all agents/proxies. Subprocesses the `wg` CLI to apply config. Deployed on `control`-role node in v1.

```
src/
├── Forculus.Domain/                        # (empty — no local state)
│
├── Forculus.Application/
│   ├── Common/
│   │   └── Interfaces/                     # IWireGuardDriver, IControlClient
│   ├── Services/
│   │   └── PeerReconciler.cs               # periodic diff: desired vs current
│   ├── Commands/
│   │   └── Peers/
│   │       ├── ApplyFullConfigCommand.cs   # full replacement on boot + reconcile
│   │       ├── UpsertPeerCommand.cs
│   │       └── RemovePeerCommand.cs
│   └── Queries/
│       └── Tunnels/GetTunnelStatsQuery.cs
│
├── Forculus.Infrastructure/
│   ├── WireGuard/
│   │   ├── WgCliDriver.cs                  # subprocess `wg`/`wg-quick`
│   │   └── ConfigWriter.cs                 # renders wg0.conf
│   └── Control/
│       └── LimenHttpClient.cs              # pulls full config on boot + every 60s reconcile
│
├── Forculus.API/                           # minimal HTTP API: /peers, /config, /stats
└── Forculus.Tests/
```

### 6.4 — `limentinus` (universal node agent)

**Role:** Runs on every node. Enrolls, maintains WG tunnel, keeps WS stream open to Limen. Loads role-specific modules.

```
src/
├── Limentinus.Domain/
│   └── Node/
│       ├── NodeIdentity.cs                 # enrollment identity (local file, mode 0600)
│       └── RoleSet.cs                      # parsed LIMEN_ROLES
│
├── Limentinus.Application/
│   ├── Common/
│   │   └── Interfaces/                     # IDockerDriver, IOstiariusSupervisor, IControlClient, IWireGuardClient
│   ├── Services/
│   │   ├── EnrollmentService.cs            # one-shot provisioning key → permanent ID/Secret
│   │   ├── HeartbeatService.cs
│   │   └── DeployPipeline.cs               # explicit stages, not a god class
│   ├── Commands/
│   │   ├── Docker/
│   │   │   ├── DeployServiceCommand.cs     # triggers DeployPipeline
│   │   │   ├── StopServiceCommand.cs
│   │   │   ├── RollbackServiceCommand.cs
│   │   │   └── StreamContainerLogsCommand.cs
│   │   └── Proxy/
│   │       ├── StartOstiariusCommand.cs
│   │       └── StopOstiariusCommand.cs
│   └── Queries/
│       ├── Docker/
│       │   └── ListRunningContainersQuery.cs
│       └── Node/
│           └── GetNodeStatusQuery.cs
│
├── Limentinus.Infrastructure/
│   ├── Docker/
│   │   └── DockerDotNetDriver.cs           # Docker.DotNet wrapper
│   ├── Proxy/
│   │   └── OstiariusDockerSupervisor.cs    # manages local Ostiarius container
│   ├── Tunnel/
│   │   └── WireGuardGoClient.cs            # embedded wireguard-go + UAPI socket
│   └── Control/
│       └── LimenWebSocketChannel.cs        # JSON/WS with auto-reconnect (1s→60s backoff)
│
├── Limentinus.API/                         # Worker Service host, Program.cs
└── Limentinus.Tests/
```

### 6.5 — DeployPipeline (explicit stages)

Coolify's `ApplicationDeploymentJob` is 1000+ LoC in one file. We avoid that with explicit stage composition:

```csharp
// Limentinus.Application/Services/DeployPipeline.cs
public sealed class DeployPipeline
{
    public async Task<DeployResult> RunAsync(DeployRequest req, CancellationToken ct)
    {
        var ctx = new DeployContext(req);
        foreach (var stage in new IDeployStage[]
        {
            new PullImageStage(_docker, _reporter),
            new CaptureOldContainerStage(_docker),
            new StartNewContainerStage(_docker, _reporter),
            new HealthCheckStage(_reporter, req.HealthCheck),
            new FinalizeStage(_docker, _reporter),        // stop old, promote new
        })
        {
            var result = await stage.ExecuteAsync(ctx, ct);
            if (result.IsFailure)
                return await RollbackAsync(ctx, ct);
        }
        return DeployResult.Success();
    }
}
```

Each stage is a small class, independently testable, composable. Reporter streams progress back to Limen via WS.

## 7. Data flows

### 7.1 — Node enrollment (Newt pattern)

```
[Admin UI → "Add node" → picks roles → receives compose snippet with provisioning key]
    ▼
[Limen] creates PendingNode + single-use ProvisioningKey (TTL 15 min)
    ▼
[Admin runs `docker compose up` on target host]
    ▼
[Limentinus] reads LIMEN_PROVISIONING_KEY env; no local identity → registers:
    WSS upgrade to wss://limen/api/agents/ws with provisioning key in initial header
    ▼
[Limen]
    - Validates provisioning key (one-shot, unexpired)
    - Creates Agent row; returns { agentId, permanentSecret }
    - Generates WG keypair for the agent's tunnel
    - POSTs peer update to Forculus: POST /peers { pubkey, allowedIps }
    ▼
[Limentinus]
    - Writes identity to local file (mode 0600)
    - Brings up wireguard-go via UAPI socket; handshake with Forculus
    - Opens permanent WS to Limen using { agentId, secret }
    - Receives current ConfigVersion + desired state
    - If 'proxy' in roles: brings up local Ostiarius container
    ▼
[Admin UI] node shown as "active"
```

### 7.2 — Expose a service on a public hostname

```
[Admin: "Expose grafana on app.example.com via proxy node edge-fra-1"]
    ▼
[Limen] AddRouteCommand handler:
    - Persist PublicRoute row
    - Persist ResourceAuthPolicy row (mode picked in UI)
    - Validate edge-fra-1 is proxy-role and online
    - Compute upstream (Agent WG IP + service port)
    ▼
[Limen → Ostiarius on edge-fra-1 via WS]
    Envelope { Type=ApplyRouteSet, Version=N+1, Payload=[full route list for this proxy] }
    ▼
[Ostiarius]
    - RouteStore.Update → YARP InMemoryConfigProvider reloads
    - CertificateRenewalService triggers LE cert for new hostname
    - Acks back { Version=N+1 }
    ▼
[Limen] marks route active; UI shows "point DNS at <edge-fra-1 IP>"
```

### 7.3 — Auto-deploy on new image (Komodo polling + Coolify queue + rollback)

```
[Quartz: RegistryPollJob; per-service cadence, default 5 min]
    - For each Service with autoDeploy=true:
        HEAD registry manifest → get digest
        compare to ServiceVersion.LastDeployedDigest
    ▼
[new digest detected]
    ▼
[Limen] CreateDeploymentCommand handler:
    - Dedup check: same (service, digest) already queued/in-progress? → return existing
    - Insert DeploymentQueue row { status=queued }
    - Promote to in-progress if node under concurrent-deploy limit
    ▼
[Limen → Limentinus via WS]
    Envelope { Type=Deploy, Payload={ deploymentId, image, env, volumes, healthcheck, rollbackPolicy } }
    ▼
[Limentinus] DeployPipeline stages:
    1. PullImage         → stream progress back
    2. CaptureOldId      → remember previous container for rollback
    3. StartNewContainer → new container with deployment-id suffix
    4. HealthCheck       → poll N times with backoff
        pass → Finalize  → stop old, promote new name, success
        fail → Rollback  → stop new, keep old running, failure
    ▼
[Limen] DeploymentQueue.status updated; logs persisted; UI updated via WS push
```

### 7.4 — Certificate renewal (Ostiarius-local)

```
[Ostiarius CertificateRenewalService (BackgroundService, 6h interval)]
    scan CertificateStore for certs expiring in <30 days
    ▼
[Per expiring cert]
    RenewCertificateCommand → LettuceEncrypt-Archon ACME (HTTP-01)
    ▼
[Success]
    overwrite cert, Kestrel hot-reloads TLS binding
    send CertificateRenewed event to Limen via WS
    ▼
[Failure]
    retry: 5m → 15m → 1h → 6h
    if expiry <7d, alert admin in UI; continue retrying
```

### 7.5 — Resource authentication (fast path)

```
[User hits app.example.com/dashboard]
    ▼
[Ostiarius AuthMiddleware — BEFORE YARP routing]
    - Read limen_session cookie
    - Verify JWT signature with Limen's Ed25519 public key (~50µs)
    - Check exp claim
    - Check jti against in-memory revoked list (refreshed every 30s)
    - Missing/invalid cookie → 302 to https://limen/auth/login?resource=<id>&return_to=<url>
    ▼
[Valid session]
    Inject X-Limen-User-Id, X-Limen-User-Email, X-Limen-Auth-Method, X-Limen-Resource-Id headers
    Forward through YARP → WG tunnel → backend service
```

### 7.6 — Magic-link login (allowlist auth mode example)

```
[User redirected to Limen login page; resource auth mode = allowlist]
    - Enters email → InitiateMagicLinkCommand
    ▼
[Limen]
    - Validate email is in resource.AllowlistedEmails
    - Create MagicLink row { token, resourceId, email, expiresAt=now+15m, usedAt=null }
    - Send email containing https://limen/auth/magic/<token>
    ▼
[User clicks link → VerifyMagicLinkCommand]
    - Validate token: exists, unused, not expired, matches resource
    - Mark used
    - Sign JWT (15 min TTL, Ed25519) with claims { sub=email, resourceId, authMethod=allowlist, jti, exp, iat }
    - Set cookie limen_session; domain based on resource cookie-scope
    - 302 → return_to
    ▼
[Ostiarius] cookie valid → forward with headers → backend
```

## 8. Error handling & resilience

### 8.1 — WebSocket reconnection

Agent side (Limentinus, Ostiarius):

- Exponential backoff: 1s → 2s → 5s → 15s → 30s → 60s (capped)
- Identity-based reconnect (agentId + secret)
- On reconnect: present current local ConfigVersion; server sends diff or full state if out-of-sync
- Chain-ID deduplication on in-flight commands that may retry

Server side (Limen):

- `IAgentConnection` is alive only while WS is open
- Disconnected agent → UI shows stale
- No server-initiated retries; agents always redial
- No reconnect for >5 min → agent marked offline, auto-deploys paused for that node

### 8.2 — Forculus HTTP resilience

Fixes Gerbil's known weakness (no reconnect after Pangolin restart):

- Forculus runs a 60s reconcile loop: `GET /api/forculus/config` from Limen, applies diffs via `wg syncconf`
- Limen push failures retry via Quartz: 5m → 15m → 30m
- Forculus persists nothing; full config always pulled on boot

### 8.3 — Deploy failure matrix

| Failure | Behavior |
|---------|----------|
| Image pull fails | Deployment marked failed, no container changes, old version untouched |
| New container fails to start | Rollback step no-op (old never stopped) |
| New container starts but health check fails | Stop new, keep old running, mark failed-and-rolled-back |
| Agent disconnects mid-deploy | Deployment stays `in-progress`; on reconnect, agent reports current stage; if terminal, finalize; otherwise resume from last safe checkpoint |
| First deploy (no old container) | No rollback attempted, just report failure |

### 8.4 — WireGuard tunnel failure

- Agent pings Forculus-assigned IP every 30s over the tunnel
- Three consecutive failures → tear down + re-up wireguard-go interface
- WS rides on WG; tunnel break cascades to WS reconnect

### 8.5 — Cert renewal failure

- LettuceEncrypt-Archon retries: 5m → 15m → 1h → 6h
- <7d to expiry + still failing: push alert to Limen (UI visible)
- Expired cert: Kestrel continues serving expired cert (graceful degradation > cold failure)

### 8.6 — Postgres outage

- All writes 503; agent WSes stay open but can't receive new commands
- Ostiarius continues serving traffic using in-memory route table + disk-cached certs (no DB dependency at runtime — data plane unaffected by control-plane DB outage)
- Cert validation continues with cached public key + stale revocation list

## 9. Testing strategy

### 9.1 — Unit tests

| Layer | What is tested | Framework |
|-------|----------------|-----------|
| Domain | Nothing — just POCOs | n/a |
| Application | Every command/query handler + every service | xUnit + FluentAssertions + NSubstitute |
| Infrastructure | Thin adapters — tested via integration | minimal here |

Mediator pipeline behaviors (validation, logging) get handler-agnostic tests.

### 9.2 — Integration tests

- **Limen:** Testcontainers-style Postgres per test class; real EF, real migrations, real Quartz (manually triggered); mocked `IAgentConnection`, `IProxyConnection`, `IForculusClient`
- **Ostiarius:** `WebApplicationFactory` + YARP + test upstream; real LettuceEncrypt-Archon against Pebble (LE test server in container)
- **Limentinus:** `Testcontainers.Docker` for real Docker-in-Docker deploy flows
- **Forculus:** `FakeWgDriver` implements `IWireGuardDriver`, asserts intended `wg` invocations (don't run actual WG in tests)

### 9.3 — E2E

Under `tests/e2e/`: one compose.yml brings up all four components + Postgres + fake registry + Pebble + a "fake remote node" (second limentinus container). Suite covers:

- Full enrollment flow
- Add service + route → public route reachable
- Push fake image → auto-deploy → container runs on target node
- Kill agent mid-deploy → verify deployment status resolves on reconnect
- Cert renewal via Pebble
- Auth flows: password login, magic link, OIDC callback (against a test IdP)

### 9.4 — Frontend

Angular tests colocated with components (Kursa pattern): `karma` + `jasmine` for units; `Playwright` smoke test per feature area.

### 9.5 — CI pipeline (per repo in `getlimen/`)

1. Build + unit/integration tests (with Testcontainers)
2. AOT compile (catches reflection-accidents early)
3. Build Docker image
4. Push to `ghcr.io/getlimen/*:sha-<short>` on main
5. Nightly + pre-release: full E2E suite against real compose

## 10. Resource-level authentication

See §7.5 and §7.6 for flows. Design principles:

- **Fast path is local:** Ostiarius verifies Ed25519 signatures locally; only login flow hits Limen.
- **Short-lived JWTs (15 min):** silent refresh via `/__limen/auth/refresh`.
- **Sub-minute revocation:** Ostiarius polls `/api/auth/revoked` every 30s for a list of active-but-revoked JTIs.
- **Header injection:** `X-Limen-User-{Id,Email}`, `X-Limen-Auth-Method`, `X-Limen-Resource-Id` — enables zero-config SSO for apps like Grafana, Gitea, Jellyfin that accept header-based auth.
- **Cookie scoping:** per-route choice of `strict` (exact hostname) or `domain` (`.example.com` apex — shared SSO).

v1 modes: `none`, `password`, `sso` (OIDC), `allowlist` (magic link).
v2+: PIN, API tokens, WebAuthn, granular per-path rules.

## 11. Repositories & org layout

**Org:** `github.com/getlimen`

```
github.com/getlimen/
├── limen         — central manager (C# + Angular + Postgres). Hosts Limen.Contracts NuGet source.
├── ostiarius     — reverse proxy (C# NativeAOT, YARP, LettuceEncrypt-Archon)
├── forculus     — WG hub (C# NativeAOT, `wg` CLI subprocess)
├── limentinus    — universal node agent (C# NativeAOT, wireguard-go userspace)
├── limen-cli     — admin CLI (v1.1+ deferred)
├── limen-docs    — docs site (later)
└── .github       — org-level templates (issue/PR templates, profile README)
```

Each repo follows the same structure rules (clean architecture layers, `Commands/`/`Queries/` one-file convention, Apache 2.0, English-only).

Commit convention (inherited from other PianoNic repos): **no AI/Claude attribution in commits or PRs**. No `Co-Authored-By: Claude`, no 🤖 trailers.

## 12. Open questions (to resolve during implementation)

- **JWT revocation storage growth:** In-memory revoked-JTI list is fine for v1 scale; does it need pagination/expiry beyond natural `exp` cleanup? Decide during implementation.
- **Cookie SameSite for cross-subdomain SSO:** `Lax` works; is `Strict` better for any mode? Revisit during auth implementation.
- **Registry credentials storage:** Private registries need credentials. Store encrypted in Postgres (column-level encryption with app-held key)? Or use system keyring on control-plane host? Decide during registry integration.
- **`wg` vs `wg-quick` in Forculus:** `wg syncconf` is cleaner for reconciliation than `wg-quick down/up`. Test in prototype.
- **Ostiarius public-key caching strategy:** Fetch on boot + poll periodically, or receive via WS push on rotation? Probably WS push with boot fallback.

## 13. References

Architectures studied during design:

- **Coolify** (`coollabsio/coolify`) — deploy queue + status tracking pattern, domain-organized actions, Traefik-via-labels, SSH remote execution (we don't copy the SSH approach, but the queue design is adopted)
- **Komodo** (`moghtech/komodo`) — Core/Periphery split, stateless agent pattern, asymmetric auth (noted; we use simpler JWT + provisioning key)
- **Pangolin** (`fosrl/pangolin`) — overall hub-and-spoke shape, resource-level auth concept, Traefik delegation (we build our own proxy instead)
- **Gerbil** (`fosrl/gerbil`) — WG relay via `wgctrl-go` + HTTP push/pull hybrid (we use `wg` CLI subprocess)
- **Newt** (`fosrl/newt`) — two-tier credential (provisioning key → permanent), persistent WS, userspace wireguard-go, chain-ID dedup (adopted wholesale)
- **Portainer** (`portainer/portainer`) — Edge Agent pull model, EdgeKey composite token, ECDSA signed requests, polling-over-WebSocket (informed our choice to prefer WS-push over polling, given Limen's smaller scale)

All clones under `C:\GithubProjects\_research\<name>` for reference.
